using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for cartridge refill tracking operations
    /// </summary>
    public class CartridgeRefillRepository
    {
        /// <summary>
        /// Retrieves refill eligibility for all vendor cartridge batches
        /// BATCH-ONLY QUERY: Reads only from VendorCartridgeBatch table
        /// Joins to CartridgeModel ONLY for display (ModelNumber)
        /// Does NOT join to Cartridge or CartridgeMovement tables
        /// </summary>
        public async Task<List<RefillEligibilityResult>> GetRefillEligibilityAsync()
        {
            var results = new List<RefillEligibilityResult>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // SCHEMA: VendorCartridgeBatch now uses CartridgeModelId (FK to CartridgeModel)
                // JOIN to CartridgeModel to get ModelNumber for display only
                var sql = @"
                    SELECT
                        vcb.BatchId,
                        v.VendorName,
                        -- CartridgeModel: prefer batch lines (always populated at creation),
                        -- enrich with any additional models found in assigned EmptyCartridge rows,
                        -- fall back to the batch header's single CartridgeModelId.
                        COALESCE(
                            NULLIF(
                                (SELECT STRING_AGG(ModelNumber, ', ') WITHIN GROUP (ORDER BY ModelNumber)
                                 FROM (
                                     -- Union: batch lines (set at creation) + any extra from assigned empties
                                     SELECT DISTINCT cm2.ModelNumber
                                     FROM dbo.VendorCartridgeBatchLine vbl2
                                     INNER JOIN dbo.CartridgeModel cm2 ON vbl2.CartridgeModelId = cm2.CartridgeModelId
                                     WHERE vbl2.BatchId = vcb.BatchId
                                       AND ISNULL(cm2.IsRefillable, 1) = 1
                                     UNION
                                     SELECT DISTINCT cm2.ModelNumber
                                     FROM dbo.EmptyCartridge ec2
                                     INNER JOIN dbo.CartridgeModel cm2 ON ec2.CartridgeModelId = cm2.CartridgeModelId
                                     WHERE ec2.VendorBatchId = vcb.BatchId
                                       AND ISNULL(cm2.IsRefillable, 1) = 1
                                 ) AS dist_models),
                                ''
                            ),
                            ISNULL(cm.ModelNumber, '[Missing CartridgeModelId]')
                        ) AS CartridgeModel,
                        vcb.OriginalQty,
                        vcb.ReturnedQty,
                        (SELECT STRING_AGG(sub.ModelSummary, CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY sub.ModelNumber)
                         FROM (SELECT cm2.ModelNumber,
                                      cm2.ModelNumber + ' : ' + CAST(SUM(ec2.Quantity) AS VARCHAR(10)) AS ModelSummary
                               FROM dbo.EmptyCartridge ec2
                               INNER JOIN dbo.CartridgeModel cm2 ON ec2.CartridgeModelId = cm2.CartridgeModelId
                               WHERE ec2.VendorBatchId = vcb.BatchId
                                 AND ISNULL(cm2.IsRefillable, 1) = 1
                               GROUP BY cm2.ModelNumber) AS sub
                        ) AS QtyPerCartridge,
                        vcb.Status,
                        vcb.CartridgeModelId,
                        ISNULL(cm.IsRefillable, 1) AS IsRefillable
                    FROM dbo.VendorCartridgeBatch vcb
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    LEFT JOIN dbo.CartridgeModel cm ON vcb.CartridgeModelId = cm.CartridgeModelId
                    WHERE v.IsRefiller = 1
                      AND vcb.Status IN ('Active', 'ForReturn', 'Closed', 'SentForRefill', 'Completed')
                      AND ISNULL(vcb.BatchPurpose, 'REFILL') = 'REFILL'
                    ORDER BY
                        CASE vcb.Status
                            WHEN 'Active' THEN 1
                            WHEN 'ForReturn' THEN 2
                            WHEN 'SentForRefill' THEN 3
                            WHEN 'Completed' THEN 4
                            WHEN 'Closed' THEN 5
                        END,
                        v.VendorName,
                        ISNULL(cm.ModelNumber, '')";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new RefillEligibilityResult
                        {
                            BatchId = reader.GetInt32(0),
                            VendorName = reader.GetString(1),
                            CartridgeModel = reader.GetString(2),
                            OriginalQty = reader.GetInt32(3),
                            ReturnedQty = reader.GetInt32(4),
                            QtyPerCartridge = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            Status = reader.GetString(6),
                            CartridgeModelId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                            IsRefillable = !reader.IsDBNull(8) && reader.GetBoolean(8)
                        });
                    }
                }
            }

            return results;
        }

        public async Task<List<int>> GetEligibleRefillEmptyCartridgeIdsAsync(IEnumerable<int> emptyCartridgeIds)
        {
            var ids = new List<int>(emptyCartridgeIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) return new List<int>();

            var inParams = ids.Select((_, i) => $"@p{i}").ToList();
            string sql = $@"
                SELECT ec.EmptyCartridgeId
                FROM dbo.EmptyCartridge ec
                LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                WHERE ec.EmptyCartridgeId IN ({string.Join(", ", inParams)})
                  AND ISNULL(cm.IsRefillable, 1) = 1";

            var results = new List<int>();
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue(inParams[i], ids[i]);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Throws InvalidOperationException if the cartridge model linked to this batch
        /// has IsRefillable == false. Must be called before Send to Vendor and Confirm Refilled Return.
        /// </summary>
        public async Task AssertBatchCartridgeModelIsRefillableAsync(int batchId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                // Multi-model aware: a refill batch may include multiple CartridgeModelIds.
                // Validate ALL models tied to this batch (prefer batch lines when available).
                int hasLinesTable = 0;
                using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.VendorCartridgeBatchLine','U') IS NULL THEN 0 ELSE 1 END", con))
                {
                    var obj = await cmd.ExecuteScalarAsync();
                    hasLinesTable = obj == null ? 0 : Convert.ToInt32(obj);
                }

                string nonRefillableSql = hasLinesTable == 1
                    ? @"
                        WITH ModelIds AS (
                            SELECT vcb.CartridgeModelId
                            FROM dbo.VendorCartridgeBatch vcb
                            WHERE vcb.BatchId = @BatchId
                            UNION
                            SELECT vbl.CartridgeModelId
                            FROM dbo.VendorCartridgeBatchLine vbl
                            WHERE vbl.BatchId = @BatchId
                        )
                        SELECT cm.ModelNumber
                        FROM ModelIds mi
                        INNER JOIN dbo.CartridgeModel cm ON mi.CartridgeModelId = cm.CartridgeModelId
                        WHERE ISNULL(cm.IsRefillable, 1) = 0"
                    : @"
                        SELECT cm.ModelNumber
                        FROM dbo.VendorCartridgeBatch vcb
                        INNER JOIN dbo.CartridgeModel cm ON vcb.CartridgeModelId = cm.CartridgeModelId
                        WHERE vcb.BatchId = @BatchId
                          AND ISNULL(cm.IsRefillable, 1) = 0";

                var badModels = new List<string>();
                using (var cmd = new SqlCommand(nonRefillableSql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                            badModels.Add(reader.IsDBNull(0) ? "[Unknown]" : reader.GetString(0));
                    }
                }

                if (badModels.Count > 0)
                    throw new InvalidOperationException(
                        $"Batch {batchId} contains non-refillable cartridge model(s): {string.Join(", ", badModels)}. " +
                        "Non-refillable cartridges must be disposed of or sold internally by IT.");
            }
        }

        private sealed class BatchModelQty
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
            public int Quantity { get; set; }
        }

        private async Task<List<BatchModelQty>> GetBatchModelQuantitiesAsync(SqlConnection con, SqlTransaction transaction, int batchId)
        {
            var results = new List<BatchModelQty>();

            const string sql = @"
                SELECT ec.CartridgeModelId,
                       ISNULL(cm.ModelNumber, '[Unknown]') AS ModelNumber,
                       SUM(ec.Quantity) AS Qty
                FROM dbo.EmptyCartridge ec
                LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                WHERE ec.VendorBatchId = @BatchId
                  AND ec.Status <> 'Completed'
                GROUP BY ec.CartridgeModelId, cm.ModelNumber
                HAVING SUM(ec.Quantity) > 0
                ORDER BY ISNULL(cm.ModelNumber, '')";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@BatchId", batchId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new BatchModelQty
                        {
                            CartridgeModelId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            ModelNumber = reader.IsDBNull(1) ? "[Unknown]" : reader.GetString(1),
                            Quantity = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// No-op: ThresholdMet status has been removed. Returns 0.
        /// </summary>
        public async Task<int> UpdateEligibleBatchStatusAsync()
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return 0;
        }

        public async Task UpdateBatchVendorAsync(int batchId, int newVendorId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        int cartridgeModelId;
                        string status;

                        const string getBatchSql = @"
                            SELECT CartridgeModelId, Status
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(getBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    throw new InvalidOperationException($"Batch {batchId} not found.");

                                cartridgeModelId = reader.GetInt32(0);
                                status = reader.GetString(1);
                            }
                        }

                        if (status != "Active")
                            throw new InvalidOperationException($"Batch {batchId} cannot change vendor. Current status: {status}. Expected: Active.");

                        const string validateVendorSql = @"
                            SELECT COUNT(*)
                            FROM dbo.Vendor v
                            LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                            WHERE v.VendorID = @VendorId
                              AND v.IsActive = 1
                              AND v.IsRefiller = 1
                              AND arc.ArchiveId IS NULL";

                        using (var cmd = new SqlCommand(validateVendorSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@VendorId", newVendorId);
                            var result = await cmd.ExecuteScalarAsync();
                            if (Convert.ToInt32(result) <= 0)
                                throw new InvalidOperationException($"VendorId {newVendorId} not found, inactive, or not a refill vendor.");
                        }

                        const string conflictSql = @"
                            SELECT TOP 1 BatchId
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId <> @BatchId
                              AND VendorId = @NewVendorId
                              AND CartridgeModelId = @CartridgeModelId
                              AND Status = 'Active'";

                        using (var cmd = new SqlCommand(conflictSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@NewVendorId", newVendorId);
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                            var conflict = await cmd.ExecuteScalarAsync();
                            if (conflict != null && conflict != DBNull.Value)
                            {
                                int conflictBatchId = Convert.ToInt32(conflict);
                                throw new InvalidOperationException(
                                    $"Cannot change vendor for Batch {batchId}. " +
                                    $"Another open batch already exists for this vendor/model (BatchId={conflictBatchId}).");
                            }
                        }

                        const string updateSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET VendorId = @NewVendorId
                            WHERE BatchId = @BatchId
                              AND Status = 'Active'";

                        using (var cmd = new SqlCommand(updateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@NewVendorId", newVendorId);
                            int rows = await cmd.ExecuteNonQueryAsync();
                            if (rows == 0)
                                throw new InvalidOperationException($"Batch {batchId} was not updated. It may have changed status.");
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
        /// Gets a specific vendor cartridge batch by ID
        /// </summary>
        public async Task<VendorCartridgeBatchDto> GetBatchByIdAsync(int batchId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT
                        vcb.BatchId,
                        vcb.VendorId,
                        v.VendorName AS VendorName,
                        ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                        vcb.CartridgeModelId,
                        vcb.OriginalQty,
                        vcb.ReturnedQty,
                        vcb.Status,
                        vcb.DateReceived,
                        vcb.CreatedBy,
                        vcb.CreatedDate,
                        vcb.Remarks
                    FROM dbo.VendorCartridgeBatch vcb
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    LEFT JOIN dbo.CartridgeModel cm ON vcb.CartridgeModelId = cm.CartridgeModelId
                    WHERE vcb.BatchId = @BatchId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new VendorCartridgeBatchDto
                            {
                                BatchId = reader.GetInt32(0),
                                VendorId = reader.GetInt32(1),
                                VendorName = reader.GetString(2),
                                CartridgeModel = reader.GetString(3),
                                CartridgeModelId = reader.GetInt32(4),
                                OriginalQty = reader.GetInt32(5),
                                ReturnedQty = reader.GetInt32(6),
                                Status = reader.GetString(7),
                                DateReceived = reader.GetDateTime(8),
                                CreatedBy = reader.GetInt32(9),
                                CreatedDate = reader.GetDateTime(10),
                                Remarks = reader.IsDBNull(11) ? null : reader.GetString(11)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Updates the status of a vendor cartridge batch.
        /// 
        /// REFILL-VENDOR RULE: Closed batches are immutable and must NEVER be reopened.
        /// Once a batch reaches 'Closed' status, no further modifications are allowed.
        /// Late-returned cartridges should go into the NEXT active batch instead.
        /// </summary>
        public async Task UpdateBatchStatusAsync(int batchId, string newStatus)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // REFILL-VENDOR FIX: Prevent reopening or modifying closed batches.
                // Closed batches represent completed refill sessions and are immutable.
                var sql = @"
                    UPDATE dbo.VendorCartridgeBatch
                    SET Status = @Status
                    WHERE BatchId = @BatchId
                      AND Status != 'Closed'";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                    cmd.Parameters.AddWithValue("@Status", newStatus);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"WARNING: UpdateBatchStatusAsync updated 0 rows for BatchId={batchId}, NewStatus={newStatus}. " +
                            "Batch may already be closed.");
                    }
                }
            }
        }

        /// <summary>
        /// Closes a refill batch that has been sent to vendor.
        /// Status transition: SentForRefill → Closed
        /// Also marks all linked empty cartridges as 'Closed' (terminal state).
        /// No inventory changes — refilled cartridges enter via BatchAddItemDialog.
        /// </summary>
        public async Task CloseRefillBatchAsync(int batchId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string updateBatchSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET Status    = 'Closed',
                                ClosedDate = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                                ClosedBy   = @UserId
                            WHERE BatchId = @BatchId
                              AND Status  = 'SentForRefill'";

                        int rows;
                        using (var cmd = new SqlCommand(updateBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            rows = await cmd.ExecuteNonQueryAsync();
                        }

                        if (rows == 0)
                            throw new InvalidOperationException(
                                $"Batch {batchId} was not closed. It may not exist or is not in 'SentForRefill' status.");

                        const string updateEmptiesSql = @"
                            UPDATE dbo.EmptyCartridge
                            SET Status       = 'Closed',
                                DateModified = GETDATE(),
                                ModifiedBy   = @UserId
                            WHERE VendorBatchId = @BatchId
                              AND Status = 'SentForRefill'";

                        using (var cmd = new SqlCommand(updateEmptiesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            await cmd.ExecuteNonQueryAsync();
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
        /// Creates a new refill transaction
        /// </summary>
        public async Task<int> CreateRefillTransactionAsync(RefillTransactionDto transaction)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    INSERT INTO dbo.RefillTransaction
                        (BatchId, VendorId, SentQty, SentDate, Status, CreatedBy, CreatedDate, Remarks)
                    VALUES
                        (@BatchId, @VendorId, @SentQty, @SentDate, @Status, @CreatedBy, GETDATE(), @Remarks);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", transaction.BatchId);
                    cmd.Parameters.AddWithValue("@VendorId", transaction.VendorId);
                    cmd.Parameters.AddWithValue("@SentQty", transaction.SentQty);
                    cmd.Parameters.AddWithValue("@SentDate", transaction.SentDate);
                    cmd.Parameters.AddWithValue("@Status", transaction.Status);
                    cmd.Parameters.AddWithValue("@CreatedBy", transaction.CreatedBy);
                    cmd.Parameters.AddWithValue("@Remarks", (object)transaction.Remarks ?? DBNull.Value);

                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        /// Computes the historical OriginalQty for a cartridge model.
        /// 
        /// REFILL-VENDOR DECOUPLING:
        /// VendorId in VendorCartridgeBatch represents the REFILL vendor, NOT the original supplier.
        /// Therefore, OriginalQty is computed by CartridgeModelId ONLY — it counts all cartridges
        /// of this model that ever entered inventory, regardless of which vendor supplied them.
        /// 
        /// SOURCE OF TRUTH: dbo.CartridgeMovement table
        /// INCLUDED MOVEMENTS: StockIn, RefillIn (cartridges entering inventory)
        /// EXCLUDED MOVEMENTS: Issued, Returned, Adjustment (not new stock)
        /// </summary>
        /// <param name="vendorId">IGNORED — kept for backward compatibility but not used in query.
        /// The refill vendor is NOT the same as the supplier, so filtering by VendorId is incorrect.</param>
        /// <param name="cartridgeModelId">The cartridge model ID to compute history for</param>
        /// <returns>Total quantity of StockIn + RefillIn movements for this model</returns>
        public async Task<int> ComputeOriginalQtyFromHistoryAsync(int vendorId, int cartridgeModelId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // REFILL-VENDOR FIX: Compute by CartridgeModelId only.
                // Previously filtered by i.VendorId (supplier), but the refill vendor
                // may differ from the supplier. OriginalQty tracks total cartridges of
                // this MODEL that entered inventory, regardless of supplier.
                const string sql = @"
                    SELECT ISNULL(SUM(cm.Quantity), 0)
                    FROM dbo.CartridgeMovement cm
                    INNER JOIN dbo.Item i ON cm.ItemId = i.ItemId
                    WHERE i.CartridgeModelId = @CartridgeModelId
                      AND cm.MovementType IN ('StockIn', 'RefillIn')";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        /// <summary>
        /// Finds or creates a vendor batch for a given REFILL vendor and cartridge model.
        /// Returns the BatchId of the active batch.
        /// 
        /// REFILL-VENDOR DESIGN:
        /// - VendorId here is the REFILL vendor, NOT necessarily the original supplier.
        /// - A cartridge may be supplied by Vendor A but refilled by Vendor B.
        /// - The batch groups cartridges by refill vendor + model for a refill session.
        /// - OriginalQty is computed from historical movements by CartridgeModelId only
        ///   (not filtered by supplier VendorId, since refill vendor ≠ supplier).
        /// - This value is snapshotted at batch creation and NEVER recalculated.
        /// 
        /// SCHEMA: Uses CartridgeModelId (NOT text-based CartridgeModel)
        /// </summary>
        private async Task<int> FindOrCreateBatchAsync_Unused(int vendorId, int cartridgeModelId, int originalQty, decimal returnPercent, int createdBy, string remarks = null)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // AUTO-ASSIGNMENT RULE: Only 'Active' batches below their return threshold
                // are eligible for auto-assignment (Rules 1–3).
                //
                // 'ThresholdMet' ("Eligible") and all later statuses (ForReturn, SentForRefill,
                // Completed, Closed) are excluded.  Returns arriving after the threshold is met
                // are LATE returns and must stay unassigned (VendorBatchId = NULL, Rule 4).
                var findSql = @"
                    SELECT BatchId, ReturnedQty, RequiredReturnQty
                    FROM dbo.VendorCartridgeBatch
                    WHERE VendorId = @VendorId
                      AND CartridgeModelId = @CartridgeModelId
                      AND Status = 'Active'";

                using (var cmd = new SqlCommand(findSql, con))
                {
                    cmd.Parameters.AddWithValue("@VendorId", vendorId);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int foundBatchId       = reader.GetInt32(0);
                            int existingReturned   = reader.GetInt32(1);
                            int existingRequired   = reader.GetInt32(2);

                            // RULE 2 & 3 — Late Return Guard (async path):
                            // The threshold has already been met, so this return is LATE.
                            // Return 0 to signal the caller that VendorBatchId must stay NULL.
                            // A user can manually assign it later (Rule 6).
                            if (existingReturned >= existingRequired)
                            {
                                return 0; // Late return — leave unassigned
                            }

                            return foundBatchId; // Early return: still below threshold
                        }
                    }
                }

                // REFILL-VENDOR FIX: Compute OriginalQty by CartridgeModelId only.
                // The refill vendor may differ from the supplier, so we do NOT filter
                // historical movements by VendorId. OriginalQty = total cartridges of this
                // MODEL that ever entered inventory, regardless of supplier.
                int snapshotOriginalQty = originalQty;
                if (snapshotOriginalQty <= 0)
                {
                    snapshotOriginalQty = await ComputeOriginalQtyFromHistoryAsync(vendorId, cartridgeModelId);
                }

                // If still zero (no history), estimate from current accumulation
                // REFILL-VENDOR FIX: Query pending empties by CartridgeModelId only,
                // since the EmptyCartridge.VendorId is the SUPPLIER, not the refill vendor.
                if (snapshotOriginalQty <= 0)
                {
                    const string countPendingSql = @"
                        SELECT ISNULL(SUM(Quantity), 0)
                        FROM dbo.EmptyCartridge
                        WHERE CartridgeModelId = @CartridgeModelId
                          AND Status = 'Pending'";

                    using (var cmd = new SqlCommand(countPendingSql, con))
                    {
                        cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                        var result = await cmd.ExecuteScalarAsync();
                        int currentPending = Convert.ToInt32(result);

                        // Use the larger of: current pending qty, provided qty, or reasonable default (10)
                        snapshotOriginalQty = Math.Max(Math.Max(currentPending, originalQty), 10);
                    }
                }

                // Compute RequiredQty = CEILING(OriginalQty * ExpectedReturnPercentage / 100)
                var requiredReturnQty = (int)Math.Ceiling(snapshotOriginalQty * (returnPercent / 100));

                // Try to INSERT new batch - may fail due to UNIQUE constraint if race condition occurs
                var createSql = @"
                    INSERT INTO dbo.VendorCartridgeBatch
                        (VendorId, CartridgeModelId, OriginalQty, RequiredReturnPercent, RequiredReturnQty, ReturnedQty, Status, DateReceived, CreatedBy, CreatedDate, Remarks)
                    VALUES
                        (@VendorId, @CartridgeModelId, @OriginalQty, @RequiredReturnPercent, @RequiredReturnQty, 0, 'Active', GETDATE(), @CreatedBy, GETDATE(), @Remarks);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                try
                {
                    using (var cmd = new SqlCommand(createSql, con))
                    {
                        cmd.Parameters.AddWithValue("@VendorId", vendorId);
                        cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                        cmd.Parameters.AddWithValue("@OriginalQty", snapshotOriginalQty);
                        cmd.Parameters.AddWithValue("@RequiredReturnPercent", returnPercent);
                        cmd.Parameters.AddWithValue("@RequiredReturnQty", requiredReturnQty);
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // UNIQUE constraint violation — race condition: another thread beat us.
                    // Re-select it and apply the same threshold check for consistency.
                    using (var cmd = new SqlCommand(findSql, con))
                    {
                        cmd.Parameters.AddWithValue("@VendorId", vendorId);
                        cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int raceBatchId     = reader.GetInt32(0);
                                int raceReturned    = reader.GetInt32(1);
                                int raceRequired    = reader.GetInt32(2);

                                if (raceReturned >= raceRequired)
                                {
                                    return 0; // Treat as late return — leave unassigned
                                }

                                return raceBatchId;
                            }
                        }

                        // Should never happen, but re-throw if batch still not found
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Vendor is unknown when a cartridge becomes empty — auto-assignment is not possible.
        /// Always returns 0 so the EmptyCartridge row is stored with VendorBatchId = NULL.
        /// Use the manual batch-assignment wizard to assign empties after a batch is created.
        /// </summary>
        public int TryGetEligibleBatchForReturn(int vendorId, int cartridgeModelId, SqlConnection con, SqlTransaction transaction)
        {
            return 0;
        }

        /// <summary>
        /// Vendor is unknown when a cartridge becomes empty — returns 0 (no batch assigned).
        /// Batch creation is a deliberate procurement action performed via the refill wizard.
        /// </summary>
        public int GetOrCreateActiveBatch(int vendorId, int cartridgeModelId, int createdBy, SqlConnection con, SqlTransaction transaction)
        {
            return 0;
        }


        /// <summary>
        /// Vendor is unknown when a cartridge becomes empty — returns 0 (no batch assigned).
        /// Batch creation is a deliberate procurement action performed via the refill wizard.
        /// </summary>
        public async Task<int> GetOrCreateActiveBatchAsync(int vendorId, int cartridgeModelId, int createdBy)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return 0;
        }

        // =====================================================================
        // THREE CLEARLY SEPARATED BATCH OPERATIONS
        //
        // 1. GetActiveBatchAsync     — lookup only, no side effects
        // 2. CreateRefillBatchAsync  — explicit creation, never called by return logic
        // 3. AssignReturnsToBatchAsync — manual cross-vendor assignment
        //
        // Return processing uses TryGetEligibleBatchForReturn (read-only lookup)
        // and NEVER touches CreateRefillBatchAsync or AssignReturnsToBatchAsync.
        // =====================================================================

        /// <summary>
        /// Finds the single ACTIVE vendor batch for a refill vendor + cartridge model.
        /// Pure lookup — does NOT create a batch and has no side effects.
        ///
        /// Returns:
        ///   > 0  BatchId of the existing ACTIVE batch
        ///   = 0  No active batch found for this vendor + model
        /// </summary>
        public async Task<int> GetActiveBatchAsync(int vendorId, int cartridgeModelId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT BatchId
                    FROM dbo.VendorCartridgeBatch
                    WHERE VendorId         = @VendorId
                      AND CartridgeModelId = @CartridgeModelId
                      AND Status           = 'Active'";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@VendorId",         vendorId);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                    var result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        /// <summary>
        /// Creates a new refill batch for the specified vendor and cartridge model.
        ///
        /// EXPLICIT CREATION — must only be called by deliberate user/admin action.
        /// This method must NEVER be called from cartridge-return processing.
        ///
        /// Why batch creation is kept separate:
        ///   Starting a refill cycle is a procurement decision — it selects a vendor,
        ///   commits a return quota, and initiates a vendor engagement.  Allowing a
        ///   return event to trigger this silently would bypass procurement controls.
        ///
        /// REFILL-VENDOR DESIGN:
        ///   VendorId = the REFILL vendor, NOT the original supplier.
        ///   Any vendor may refill any cartridge model, regardless of who supplied them.
        ///   OriginalQty is computed from CartridgeMovement history by CartridgeModelId
        ///   ONLY — supplier VendorId is not used in that calculation.
        ///
        /// Throws InvalidOperationException if an Active batch already exists for this
        /// vendor + cartridge model (call GetActiveBatchAsync first to check).
        ///
        /// Returns the newly created BatchId.
        /// </summary>
        public async Task<int> CreateRefillBatchAsync(int vendorId, int cartridgeModelId, int originalQty, int createdBy, string remarks = null)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // Guard: refuse to create if an Active batch already exists.
                // The caller should call GetActiveBatchAsync first and decide what to do.
                const string checkSql = @"
                    SELECT COUNT(1)
                    FROM dbo.VendorCartridgeBatch
                    WHERE VendorId         = @VendorId
                      AND CartridgeModelId = @CartridgeModelId
                      AND Status           = 'Active'";

                using (var cmd = new SqlCommand(checkSql, con))
                {
                    cmd.Parameters.AddWithValue("@VendorId",         vendorId);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                    int existing = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (existing > 0)
                    {
                        throw new InvalidOperationException(
                            $"An Active batch already exists for VendorId={vendorId}, " +
                            $"CartridgeModelId={cartridgeModelId}. " +
                            "Close or complete the existing batch before creating a new one.");
                    }
                }

                // Compute OriginalQty by CartridgeModelId only (supplier-agnostic).
                int snapshotOriginalQty = originalQty;
                if (snapshotOriginalQty <= 0)
                {
                    snapshotOriginalQty = await ComputeOriginalQtyFromHistoryAsync(vendorId, cartridgeModelId);
                }

                if (snapshotOriginalQty <= 0)
                {
                    const string pendingSql = @"
                        SELECT ISNULL(SUM(Quantity), 0)
                        FROM dbo.EmptyCartridge
                        WHERE CartridgeModelId = @CartridgeModelId
                          AND Status = 'Pending'";

                    using (var cmd = new SqlCommand(pendingSql, con))
                    {
                        cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                        int pending = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        snapshotOriginalQty = Math.Max(pending, 1);
                    }
                }

                const string insertSql = @"
                    INSERT INTO dbo.VendorCartridgeBatch
                        (VendorId, CartridgeModelId, OriginalQty, ReturnedQty, Status,
                         DateReceived, CreatedBy, CreatedDate, Remarks)
                    VALUES
                        (@VendorId, @CartridgeModelId, @OriginalQty, 0, 'Active',
                         GETDATE(), @CreatedBy, GETDATE(), @Remarks);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newBatchId;
                using (var cmd = new SqlCommand(insertSql, con))
                {
                    cmd.Parameters.AddWithValue("@VendorId",         vendorId);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                    cmd.Parameters.AddWithValue("@OriginalQty",      snapshotOriginalQty);
                    cmd.Parameters.AddWithValue("@CreatedBy",        createdBy);
                    cmd.Parameters.AddWithValue("@Remarks",          (object)remarks ?? DBNull.Value);

                    newBatchId = (int)await cmd.ExecuteScalarAsync();
                }

                // Also populate VendorCartridgeBatchLine for many-to-many support
                // (OBJECT_ID guard keeps this safe before the migration is applied)
                const string insertLineSql = @"
                    IF OBJECT_ID('dbo.VendorCartridgeBatchLine', 'U') IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM dbo.VendorCartridgeBatchLine
                            WHERE BatchId = @BatchId AND CartridgeModelId = @CartridgeModelId)
                        INSERT INTO dbo.VendorCartridgeBatchLine
                            (BatchId, CartridgeModelId, SentQty, ReturnedQty)
                        VALUES
                            (@BatchId, @CartridgeModelId, @SentQty, 0)
                    END";

                using (var cmd = new SqlCommand(insertLineSql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId",          newBatchId);
                    cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                    cmd.Parameters.AddWithValue("@SentQty",          snapshotOriginalQty);
                    await cmd.ExecuteNonQueryAsync();
                }

                return newBatchId;
            }
        }

        /// <summary>
        /// Creates a multi-model refill batch for a single vendor.
        /// 
        /// Real-world shipment behavior: Multiple cartridge models are boxed together
        /// and shipped as one batch to a single refiller, not separated per model.
        /// 
        /// Creates:
        ///   - One VendorCartridgeBatch header (CartridgeModelId = first model for backward compat)
        ///   - One VendorCartridgeBatchLine per model with SentQty
        /// 
        /// The modelGroups parameter should be an IEnumerable of objects with:
        ///   - CartridgeModelId (int)
        ///   - TotalQty (int)
        /// 
        /// Returns the newly created BatchId.
        /// </summary>
        public async Task<int> CreateMultiModelRefillBatchAsync<T>(
            int vendorId,
            IEnumerable<T> modelGroups,
            int createdBy,
            string remarks = null) where T : class
        {
            var groups = modelGroups.ToList();
            if (groups.Count == 0)
                throw new ArgumentException("At least one model group is required.");

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // Auto-migrate: relax SentQty constraint from > 0 to >= 0 so that
                // empty batches (created before returns are assigned) can store model lines.
                const string migrateConstraintSql = @"
                    IF EXISTS (
                        SELECT 1 FROM sys.check_constraints c
                        INNER JOIN sys.tables t ON c.parent_object_id = t.object_id
                        WHERE t.name = 'VendorCartridgeBatchLine'
                          AND c.name = 'CK_VendorCartridgeBatchLine_SentQty'
                          AND c.definition = '([SentQty]>(0))')
                    BEGIN
                        ALTER TABLE dbo.VendorCartridgeBatchLine
                            DROP CONSTRAINT CK_VendorCartridgeBatchLine_SentQty;
                        ALTER TABLE dbo.VendorCartridgeBatchLine
                            ADD CONSTRAINT CK_VendorCartridgeBatchLine_SentQty CHECK (SentQty >= 0);
                    END";
                using (var cmd = new SqlCommand(migrateConstraintSql, con))
                    await cmd.ExecuteNonQueryAsync();

                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Use reflection to extract CartridgeModelId and TotalQty from the generic type
                        var modelIdProp = typeof(T).GetProperty("CartridgeModelId");
                        var totalQtyProp = typeof(T).GetProperty("TotalQty");

                        if (modelIdProp == null || totalQtyProp == null)
                            throw new InvalidOperationException(
                                "Model groups must have CartridgeModelId and TotalQty properties.");

                        // Extract first model for the batch header (backward compatibility)
                        int firstModelId = (int)modelIdProp.GetValue(groups[0]);
                        int totalOriginalQty = groups.Sum(g => (int)totalQtyProp.GetValue(g));

                        // Create the batch header
                        const string insertBatchSql = @"
                            INSERT INTO dbo.VendorCartridgeBatch
                                (VendorId, CartridgeModelId, OriginalQty, ReturnedQty, Status,
                                 DateReceived, CreatedBy, CreatedDate, Remarks)
                            VALUES
                                (@VendorId, @CartridgeModelId, @OriginalQty, 0, 'Active',
                                 GETDATE(), @CreatedBy, GETDATE(), @Remarks);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newBatchId;
                        using (var cmd = new SqlCommand(insertBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@VendorId", vendorId);
                            cmd.Parameters.AddWithValue("@CartridgeModelId", firstModelId);
                            cmd.Parameters.AddWithValue("@OriginalQty", totalOriginalQty);
                            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                            cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);

                            newBatchId = (int)await cmd.ExecuteScalarAsync();
                        }

                        // Insert VendorCartridgeBatchLine rows for each model.
                        // SentQty = 0 is valid for empty batches (constraint relaxed to >= 0).
                        const string insertLineSql = @"
                            IF OBJECT_ID('dbo.VendorCartridgeBatchLine', 'U') IS NOT NULL
                            BEGIN
                                IF NOT EXISTS (
                                    SELECT 1 FROM dbo.VendorCartridgeBatchLine
                                    WHERE BatchId = @BatchId AND CartridgeModelId = @CartridgeModelId)
                                INSERT INTO dbo.VendorCartridgeBatchLine
                                    (BatchId, CartridgeModelId, SentQty, ReturnedQty)
                                VALUES
                                    (@BatchId, @CartridgeModelId, @SentQty, 0)
                            END";

                        foreach (var group in groups)
                        {
                            int modelId = (int)modelIdProp.GetValue(group);
                            int qty = (int)totalQtyProp.GetValue(group);

                            using (var cmd = new SqlCommand(insertLineSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BatchId", newBatchId);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", modelId);
                                cmd.Parameters.AddWithValue("@SentQty", qty);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return newBatchId;
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
        /// Assigns a set of unassigned empty cartridges to a refill batch.
        /// For use by manual assignment flows only — NOT called during return processing.
        ///
        /// CROSS-VENDOR ASSIGNMENT (Rule 7 & 8):
        ///   The original VendorId on each EmptyCartridge row records the SUPPLIER that
        ///   issued the cartridge (for audit traceability).  The refill batch belongs to
        ///   a separate REFILL vendor, which may be completely different.
        ///   This method does NOT check supplier identity — any returned cartridge of
        ///   the correct model can be assigned to any refill batch.
        ///
        /// NEVER overwrites an existing assignment (Rule 9):
        ///   Only rows where VendorBatchId IS NULL are updated.  Rows already assigned
        ///   (by auto-assignment or a prior manual operation) are left unchanged.
        ///
        /// Returns the total quantity (sum of Quantity column) that was assigned.
        /// </summary>
        public async Task<int> AssignReturnsToBatchAsync(int batchId, IEnumerable<int> emptyCartridgeIds, int userId)
        {
            var ids = new List<int>(emptyCartridgeIds).Distinct().ToList();
            if (ids.Count == 0) return 0;

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate the target batch exists and is still Active.
                        // Also read BatchPurpose so we can skip the DAMAGED guard for outbound batches.
                        const string validateBatchSql = @"
                            SELECT BatchId, ISNULL(BatchPurpose, '') AS BatchPurpose
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId
                              AND Status  = 'Active'";

                        string batchPurpose = string.Empty;
                        using (var cmd = new SqlCommand(validateBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    throw new InvalidOperationException(
                                        $"BatchId={batchId} is not an Active batch. " +
                                        "Only Active batches may receive manual cartridge assignments.");
                                batchPurpose = reader.GetString(1);
                            }
                        }

                        // Only refill batches (no BatchPurpose) must reject DAMAGED cartridges.
                        // DISPOSE and SELL outbound batches are explicitly designed to handle them.
                        bool isOutboundBatch = batchPurpose == "DISPOSE" || batchPurpose == "SELL";
                        if (!isOutboundBatch)
                        {
                            var inParams = ids.Select((_, i) => $"@p{i}").ToList();
                            var damagedSql = $@"
                                SELECT ec.EmptyCartridgeId
                                FROM dbo.EmptyCartridge ec
                                WHERE ec.EmptyCartridgeId IN ({string.Join(", ", inParams)})
                                  AND ec.ConditionStatus = 'DAMAGED'";

                            var damagedIds = new List<int>();
                            using (var cmd = new SqlCommand(damagedSql, con, transaction))
                            {
                                for (int i = 0; i < ids.Count; i++)
                                    cmd.Parameters.AddWithValue(inParams[i], ids[i]);

                                using (var reader = await cmd.ExecuteReaderAsync())
                                    while (await reader.ReadAsync())
                                        damagedIds.Add(reader.GetInt32(0));
                            }

                            if (damagedIds.Count > 0)
                                throw new InvalidOperationException(
                                    $"Cannot assign DAMAGED cartridges to a refill batch. " +
                                    $"Affected ID(s): {string.Join(", ", damagedIds)}. " +
                                    "Route DAMAGED empties through the Outbound Batch (Dispose/Sell) flow.");
                        }

                        // Assign each unassigned empty cartridge row to the batch.
                        //
                        // WHERE VendorBatchId IS NULL ensures we never overwrite an existing
                        // assignment — this enforces Rule 9 (manual override is additive only,
                        // not destructive to already-assigned rows).
                        //
                        // Cross-vendor: we do NOT filter by ec.VendorId (the supplier).
                        // The refill batch's VendorId is the REFILL vendor — completely
                        // independent of who originally supplied the cartridge.
                        const string assignSql = @"
                            UPDATE dbo.EmptyCartridge
                            SET VendorBatchId = @BatchId,
                                Status        = 'BatchAssigned',
                                DateModified  = GETDATE(),
                                ModifiedBy    = @UserId
                            WHERE EmptyCartridgeId = @EmptyCartridgeId
                              AND VendorBatchId IS NULL
                              AND Status = 'Pending'";

                        int totalQtyAssigned = 0;
                        foreach (int emptyId in ids)
                        {
                            // Read quantity first for the ReturnedQty increment below
                            const string getQtySql = @"
                                SELECT Quantity
                                FROM dbo.EmptyCartridge
                                WHERE EmptyCartridgeId = @EmptyCartridgeId
                                  AND VendorBatchId IS NULL
                                  AND Status = 'Pending'";

                            int qty = 0;
                            using (var cmd = new SqlCommand(getQtySql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyId);
                                var result = await cmd.ExecuteScalarAsync();
                                if (result != null) qty = Convert.ToInt32(result);
                            }

                            if (qty <= 0) continue; // already assigned or not found — skip

                            using (var cmd = new SqlCommand(assignSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BatchId",          batchId);
                                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyId);
                                cmd.Parameters.AddWithValue("@UserId",           userId);
                                int rows = await cmd.ExecuteNonQueryAsync();
                                if (rows <= 0)
                                {
                                    // Another process may have assigned/updated this row after we read Qty.
                                    // Do not count it as assigned.
                                    continue;
                                }
                            }

                            totalQtyAssigned += qty;
                        }

                        if (totalQtyAssigned > 0)
                        {
                            // Increment batch ReturnedQty by the total quantity just assigned.
                            // WHERE Status = 'Active' is a safety net; the batch was already
                            // validated Active above.
                            const string incrementSql = @"
                                UPDATE dbo.VendorCartridgeBatch
                                SET ReturnedQty = ReturnedQty + @Qty
                                WHERE BatchId = @BatchId
                                  AND Status  = 'Active'";

                            using (var cmd = new SqlCommand(incrementSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BatchId", batchId);
                                cmd.Parameters.AddWithValue("@Qty",     totalQtyAssigned);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return totalQtyAssigned;
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
        /// Returns all empty cartridges that have not yet been assigned to a refill batch.
        /// Used by the manual batch-assignment wizard.
        ///
        /// Filters:
        ///   VendorBatchId IS NULL  — unassigned only
        ///   Status = 'Pending'     — not yet dispatched or completed
        ///
        /// Results are ordered by CartridgeModel then ReturnedAt (oldest first) so the wizard
        /// shows the most overdue returns at the top.
        /// </summary>
        public async Task<List<UnassignedReturnDto>> GetUnassignedReturnsAsync()
        {
            var results = new List<UnassignedReturnDto>();

            // Only GOOD (or legacy unclassified) returns are eligible for refill batches.
            // Rows with ConditionStatus = 'DAMAGED' are excluded here and surfaced via
            // GetDamagedUnassignedReturnsAsync for the Damaged Empty Cartridges page.
            const string sql = @"
                SELECT
                    ec.EmptyCartridgeId,
                    ec.CartridgeModelId,
                    ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                    ec.VendorId                              AS SupplierId,
                    ISNULL(v.VendorName, '[Unknown Vendor]') AS SupplierName,
                    ec.Quantity,
                    ec.ReturnedAt,
                    ISNULL(ec.Remarks, '')                   AS Remarks,
                    ec.ConditionStatus
                FROM dbo.EmptyCartridge ec
                LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
                WHERE ec.VendorBatchId IS NULL
                  AND ec.Status        = 'Pending'
                  AND ISNULL(cm.IsRefillable, 1) = 1
                  AND (ec.ConditionStatus = 'GOOD' OR ec.ConditionStatus IS NULL)
                ORDER BY ISNULL(cm.ModelNumber, ''), ec.ReturnedAt ASC";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new UnassignedReturnDto
                        {
                            EmptyCartridgeId = reader.GetInt32(0),
                            CartridgeModelId = reader.GetInt32(1),
                            CartridgeModel   = reader.GetString(2),
                            SupplierId       = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            SupplierName     = reader.GetString(4),
                            Quantity         = reader.GetInt32(5),
                            ReturnedAt       = reader.GetDateTime(6),
                            Remarks          = reader.GetString(7),
                            ConditionStatus  = reader.IsDBNull(8) ? null : reader.GetString(8)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all returned empty cartridges whose ConditionStatus is 'DAMAGED' and
        /// have not yet been assigned to a batch or otherwise resolved.
        ///
        /// These are intentionally excluded from the refill-batch wizard.
        /// Used by the Damaged Empty Cartridges page.
        /// </summary>
        public async Task<List<UnassignedReturnDto>> GetDamagedUnassignedReturnsAsync()
        {
            var results = new List<UnassignedReturnDto>();

            const string sql = @"
                SELECT
                    ec.EmptyCartridgeId,
                    ec.CartridgeModelId,
                    ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                    ec.VendorId                              AS SupplierId,
                    ISNULL(v.VendorName, '[Unknown Vendor]') AS SupplierName,
                    ec.Quantity,
                    ec.ReturnedAt,
                    ISNULL(ec.Remarks, '')                   AS Remarks,
                    ec.ConditionStatus
                FROM dbo.EmptyCartridge ec
                LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
                WHERE ec.ConditionStatus = 'DAMAGED'
                  AND ec.VendorBatchId IS NULL
                  AND ec.Status NOT IN ('Disposed', 'Sold', 'Refilled')
                ORDER BY ISNULL(cm.ModelNumber, ''), ec.ReturnedAt ASC";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new UnassignedReturnDto
                        {
                            EmptyCartridgeId = reader.GetInt32(0),
                            CartridgeModelId = reader.GetInt32(1),
                            CartridgeModel   = reader.GetString(2),
                            SupplierId       = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            SupplierName     = reader.GetString(4),
                            Quantity         = reader.GetInt32(5),
                            ReturnedAt       = reader.GetDateTime(6),
                            Remarks          = reader.GetString(7),
                            ConditionStatus  = "DAMAGED"
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all EmptyCartridge records whose CartridgeModel has IsRefillable == false.
        /// These are never entered into the refill queue and must be handled by IT.
        /// </summary>
        public async Task<List<NonRefillableEmptyDto>> GetNonRefillableEmptiesAsync()
        {
            var results = new List<NonRefillableEmptyDto>();

            const string sql = @"
                SELECT
                    ec.EmptyCartridgeId,
                    ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                    ec.Quantity,
                    ec.Status,
                    ec.ReturnedAt,
                    ec.ReqId,
                    ISNULL(ec.Remarks, '')   AS Remarks,
                    ec.SourceItemId,
                    ec.CartridgeModelId,
                    ec.ConditionId,
                    ec.DisposalCompanyName
                FROM dbo.EmptyCartridge ec
                INNER JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                WHERE cm.IsRefillable = 0
                  AND ec.Status NOT IN ('Disposed', 'Sold')
                  AND (
                      ec.Status <> 'BatchAssigned'
                      OR EXISTS (
                          SELECT 1 FROM dbo.VendorCartridgeBatch vcb
                          WHERE vcb.BatchId = ec.VendorBatchId
                            AND ISNULL(vcb.BatchPurpose, 'REFILL') = 'REFILL'
                      )
                  )
                ORDER BY cm.ModelNumber, ec.ReturnedAt DESC";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new NonRefillableEmptyDto
                        {
                            EmptyCartridgeId    = reader.GetInt32(0),
                            CartridgeModel      = reader.GetString(1),
                            Quantity            = reader.GetInt32(2),
                            Status              = reader.GetString(3),
                            ReturnedAt          = reader.GetDateTime(4),
                            ReqId               = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                            Remarks             = reader.GetString(6),
                            SourceItemId        = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                            CartridgeModelId    = reader.GetInt32(8),
                            ConditionId         = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                            DisposalCompanyName = reader.IsDBNull(10) ? null : reader.GetString(10)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all non-refillable empty cartridges with Status = 'Sold'.
        /// Read-only — used by the Sold Cartridges audit page.
        /// </summary>
        public Task<List<ClosedEmptyCartridgeDto>> GetSoldEmptiesAsync()
            => GetClosedEmptiesAsync("Sold");

        /// <summary>
        /// Returns all non-refillable empty cartridges with Status = 'Disposed'.
        /// Read-only — used by the Disposed Cartridges audit page.
        /// </summary>
        public Task<List<ClosedEmptyCartridgeDto>> GetDisposedEmptiesAsync()
            => GetClosedEmptiesAsync("Disposed");

        /// <summary>
        /// Returns all EmptyCartridge rows where ConditionStatus = 'DAMAGED',
        /// ordered by most recent return first.
        ///
        /// These rows are permanently excluded from refill-batch assignment.
        /// Used by the Damaged Empty Cartridges read-only page.
        /// </summary>
        public async Task<List<DamagedEmptyCartridgeDto>> GetDamagedEmptiesAsync()
        {
            var results = new List<DamagedEmptyCartridgeDto>();

            const string sql = @"
                SELECT
                    ec.EmptyCartridgeId,
                    ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                    ec.Quantity,
                    ec.ReturnedAt,
                    ec.ReqId,
                    ISNULL(ec.Remarks, '')                   AS Remarks,
                    ec.DisposalCompanyName,
                    ISNULL(c.ConditionName, ec.ConditionStatus) AS ConditionName
                FROM dbo.EmptyCartridge ec
                LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                LEFT JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                WHERE (ec.ConditionStatus = 'DAMAGED' OR c.ConditionName = 'Damaged')
                  AND ec.VendorBatchId IS NULL
                  AND ec.Status NOT IN ('Disposed', 'Sold')
                ORDER BY ec.ReturnedAt DESC";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new DamagedEmptyCartridgeDto
                        {
                            EmptyCartridgeId    = reader.GetInt32(0),
                            CartridgeModel      = reader.GetString(1),
                            Quantity            = reader.GetInt32(2),
                            ReturnedAt          = reader.GetDateTime(3),
                            ReqId               = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                            Remarks             = reader.GetString(5),
                            DisposalCompanyName = reader.IsDBNull(6) ? null : reader.GetString(6),
                            ConditionName       = reader.IsDBNull(7) ? null : reader.GetString(7)
                        });
                    }
                }
            }

            return results;
        }

        private async Task<List<ClosedEmptyCartridgeDto>> GetClosedEmptiesAsync(string status)
        {
            var results = new List<ClosedEmptyCartridgeDto>();

            const string sql = @"
                SELECT
                    ec.EmptyCartridgeId,
                    ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                    ec.Quantity,
                    ec.Status,
                    ec.ReturnedAt,
                    ec.DateModified                           AS ClosedAt,
                    ec.ReqId,
                    ISNULL(ec.Remarks, '')                   AS Remarks,
                    ec.DisposalCompanyName
                FROM dbo.EmptyCartridge ec
                INNER JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                WHERE ec.Status = @Status
                ORDER BY ec.DateModified DESC, cm.ModelNumber";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new ClosedEmptyCartridgeDto
                            {
                                EmptyCartridgeId    = reader.GetInt32(0),
                                CartridgeModel      = reader.GetString(1),
                                Quantity            = reader.GetInt32(2),
                                Status              = reader.GetString(3),
                                ReturnedAt          = reader.GetDateTime(4),
                                ClosedAt            = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                ReqId               = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                Remarks             = reader.GetString(7),
                                DisposalCompanyName = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns non-refillable empty cartridges aggregated by ReqId + CartridgeModelId.
        /// Each row in the result represents one request/model combination and carries the
        /// SUM(Quantity) and row count of the underlying dbo.EmptyCartridge rows.
        ///
        /// Presentation layer uses this for the grouped grid view.
        /// The underlying rows are untouched; Bulk actions re-expand them per row.
        /// </summary>
        public async Task<List<NonRefillableGroupedDto>> GetNonRefillableGroupedAsync()
        {
            var results = new List<NonRefillableGroupedDto>();

            // TEMPORARY: UNION ALL fans each ReqId+Model group into two rows —
            // one for GOOD (condition NULL or not 'Damaged') and one for DAMAGED.
            // Rows with zero quantity in a partition are suppressed by HAVING.
            // To revert: replace with the original single GROUP BY query.
            const string sql = @"
                SELECT ReqId, CartridgeModelId, CartridgeModel,
                       TotalQuantity, [RowCount], ReturnedAt, ConditionType
                FROM (
                    -- GOOD partition: ConditionId IS NULL or condition <> 'Damaged'
                    SELECT
                        ec.ReqId,
                        ec.CartridgeModelId,
                        ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                        SUM(ec.Quantity)                          AS TotalQuantity,
                        COUNT(*)                                  AS [RowCount],
                        MIN(ec.ReturnedAt)                        AS ReturnedAt,
                        'GOOD'                                    AS ConditionType
                    FROM dbo.EmptyCartridge ec
                    INNER JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                    LEFT  JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                    WHERE cm.IsRefillable = 0
                      AND ec.Status NOT IN ('Disposed', 'Sold')
                      AND (
                          ec.Status <> 'BatchAssigned'
                          OR EXISTS (
                              SELECT 1 FROM dbo.VendorCartridgeBatch vcb
                              WHERE vcb.BatchId = ec.VendorBatchId
                                AND ISNULL(vcb.BatchPurpose, 'REFILL') = 'REFILL'
                          )
                      )
                      AND ISNULL(c.ConditionName, '') <> 'Damaged'
                    GROUP BY ec.ReqId, ec.CartridgeModelId, cm.ModelNumber
                    HAVING SUM(ec.Quantity) > 0

                    UNION ALL

                    -- DAMAGED partition: condition is explicitly 'Damaged'
                    SELECT
                        ec.ReqId,
                        ec.CartridgeModelId,
                        ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                        SUM(ec.Quantity)                          AS TotalQuantity,
                        COUNT(*)                                  AS [RowCount],
                        MIN(ec.ReturnedAt)                        AS ReturnedAt,
                        'DAMAGED'                                 AS ConditionType
                    FROM dbo.EmptyCartridge ec
                    INNER JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                    INNER JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                    WHERE cm.IsRefillable = 0
                      AND ec.Status NOT IN ('Disposed', 'Sold')
                      AND (
                          ec.Status <> 'BatchAssigned'
                          OR EXISTS (
                              SELECT 1 FROM dbo.VendorCartridgeBatch vcb
                              WHERE vcb.BatchId = ec.VendorBatchId
                                AND ISNULL(vcb.BatchPurpose, 'REFILL') = 'REFILL'
                          )
                      )
                      AND c.ConditionName = 'Damaged'
                    GROUP BY ec.ReqId, ec.CartridgeModelId, cm.ModelNumber
                    HAVING SUM(ec.Quantity) > 0
                ) [combined]
                ORDER BY ReturnedAt DESC, CartridgeModel, ConditionType";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new NonRefillableGroupedDto
                        {
                            ReqId            = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0),
                            CartridgeModelId = reader.GetInt32(1),
                            CartridgeModel   = reader.GetString(2),
                            TotalQuantity    = reader.GetInt32(3),
                            RowCount         = reader.GetInt32(4),
                            ReturnedAt       = reader.GetDateTime(5),
                            ConditionType    = reader.GetString(6)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Bulk Dispose or Sell for all pending EmptyCartridge rows that share the same
        /// ReqId + CartridgeModelId group.  Runs inside a single SQL transaction so the
        /// operation is all-or-nothing.
        ///
        /// Per-row processing (Option A — one inventory entry per EmptyCartridge row):
        ///   1. Load Quantity, SourceItemId, ConditionId, current Status for the row.
        ///   2. Update EmptyCartridge.Status + DisposalCompanyName.
        ///   3. Decrement Item.StockOnHand (skipped when SourceItemId is missing).
        ///   4. Insert dbo.Inventory 'Negative' entry.
        ///   5. Insert dbo.CartridgeMovement 'Adjustment' entry.
        ///   6. Insert dbo.ArchiveStatus 'EmptyCartridge' record (idempotent).
        ///
        /// Rows already in 'Disposed' or 'Sold' status are skipped — they will not
        /// cause the transaction to fail.  If zero rows are actionable, an exception
        /// is thrown so the caller knows nothing was processed.
        ///
        /// The CartridgeTypeId used for CartridgeMovement is resolved once before the
        /// loop — it is the same for all rows in the group.
        /// </summary>
        private async Task BulkMarkEmptyCartridgeActionAsync(
            int? reqId, int cartridgeModelId, string action, int userId,
            string disposalCompanyName = null, string conditionType = null)
        {
            if (action != "Disposed" && action != "Sold")
                throw new ArgumentException($"Invalid action '{action}'. Must be 'Disposed' or 'Sold'.");
            if (cartridgeModelId <= 0)
                throw new ArgumentException("CartridgeModelId is required for bulk action.");
            if (action == "Sold" && conditionType == "DAMAGED")
                throw new InvalidOperationException("Bulk Sell is not allowed for DAMAGED cartridges.");

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // ── 0a. Resolve CartridgeTypeId once (reused for every movement row) ─
                        int cartridgeTypeId = 1;
                        const string cartridgeTypeSql = @"
                            SELECT TOP 1 CartridgeTypeId
                            FROM   dbo.CartridgeType
                            WHERE  CartridgeTypeName = 'Brand New'
                            ORDER  BY CartridgeTypeId ASC";

                        using (var cmd = new SqlCommand(cartridgeTypeSql, con, transaction))
                        {
                            var ctResult = await cmd.ExecuteScalarAsync();
                            if (ctResult != null && ctResult != DBNull.Value)
                                cartridgeTypeId = Convert.ToInt32(ctResult);
                        }

                        // ── 0b. Select all pending EmptyCartridgeIds for this group ────────
                        // Done inside the transaction to prevent races.
                        // TEMPORARY: conditionType ("GOOD"/"DAMAGED") narrows the selection
                        // to the matching condition partition. null = no filter (original behavior).
                        var emptyIds = new List<int>();
                        const string selectIdsSql = @"
                            SELECT ec.EmptyCartridgeId
                            FROM   dbo.EmptyCartridge ec
                            LEFT JOIN dbo.Condition c ON ec.ConditionId = c.ConditionId
                            WHERE  ec.CartridgeModelId = @CartridgeModelId
                              AND  (  (@ReqId IS NULL AND ec.ReqId IS NULL)
                                   OR ec.ReqId = @ReqId )
                              AND  ec.Status NOT IN ('Disposed', 'Sold')
                              AND  (
                                       @ConditionType IS NULL
                                   OR (@ConditionType = 'DAMAGED' AND c.ConditionName = 'Damaged')
                                   OR (@ConditionType = 'GOOD'    AND ISNULL(c.ConditionName, '') <> 'Damaged')
                              )";

                        using (var cmd = new SqlCommand(selectIdsSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                            cmd.Parameters.AddWithValue("@ReqId",
                                reqId.HasValue ? (object)reqId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ConditionType",
                                conditionType != null ? (object)conditionType : DBNull.Value);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                    emptyIds.Add(reader.GetInt32(0));
                            }
                        }

                        if (emptyIds.Count == 0)
                            throw new InvalidOperationException(
                                "No pending empty cartridge rows found for this group. " +
                                "They may have already been actioned.");

                        // ── Per-row loop ─────────────────────────────────────────────────────
                        // Same 6 steps as MarkEmptyCartridgeActionAsync, shared transaction.
                        foreach (int emptyCartridgeId in emptyIds)
                        {
                            // ── 1. Load row data ────────────────────────────────────────────
                            int    quantity         = 0;
                            int?   sourceItemId     = null;
                            int?   rowReqId         = null;
                            int?   conditionId      = null;
                            int    rowModelId       = 0;

                            const string selectRowSql = @"
                                SELECT ec.Quantity, ec.SourceItemId, ec.ReqId,
                                       ec.ConditionId, ec.CartridgeModelId
                                FROM   dbo.EmptyCartridge ec
                                WHERE  ec.EmptyCartridgeId = @EmptyCartridgeId";

                            using (var cmd = new SqlCommand(selectRowSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (!await reader.ReadAsync())
                                        throw new InvalidOperationException(
                                            $"EmptyCartridgeId {emptyCartridgeId} not found.");

                                    quantity     = reader.GetInt32(0);
                                    sourceItemId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                                    rowReqId     = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                    conditionId  = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                    rowModelId   = reader.IsDBNull(4) ? 0             : reader.GetInt32(4);
                                }
                            }

                            // ── 1b. Fallback SourceItemId lookup (legacy rows) ──────────────
                            if (sourceItemId == null && rowModelId > 0)
                            {
                                const string fallbackSql = @"
                                    SELECT TOP 1 ItemId
                                    FROM   dbo.Item
                                    WHERE  CartridgeModelId = @CartridgeModelId
                                      AND  Active           = 1
                                      AND  Category         = 'Cartridge'
                                      AND  RefillStatus     IS NULL
                                      AND  Remarks LIKE '%IT custody%'
                                    ORDER  BY DateCreated DESC";

                                using (var cmd = new SqlCommand(fallbackSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@CartridgeModelId", rowModelId);
                                    var result = await cmd.ExecuteScalarAsync();
                                    if (result != null && result != DBNull.Value)
                                        sourceItemId = Convert.ToInt32(result);
                                }
                            }

                            // ── 2. Update EmptyCartridge.Status + DisposalCompanyName ────────
                            const string updateEmptySql = @"
                                UPDATE dbo.EmptyCartridge
                                SET    Status              = @Action,
                                       DateModified        = GETDATE(),
                                       ModifiedBy          = @UserId,
                                       DisposalCompanyName = @DisposalCompanyName
                                WHERE  EmptyCartridgeId = @EmptyCartridgeId";

                            using (var cmd = new SqlCommand(updateEmptySql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Action",           action);
                                cmd.Parameters.AddWithValue("@UserId",           userId);
                                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                                cmd.Parameters.AddWithValue("@DisposalCompanyName",
                                    string.IsNullOrWhiteSpace(disposalCompanyName)
                                        ? (object)DBNull.Value
                                        : disposalCompanyName.Trim());
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ── 3-5. Inventory steps (only when IT custody Item exists) ──────
                            if (sourceItemId != null)
                            {
                                // ── 3. Decrement Item.StockOnHand ─────────────────────────
                                const string decrementItemSql = @"
                                    UPDATE dbo.Item
                                    SET    StockOnHand  = StockOnHand - @Quantity,
                                           DateModified = GETDATE(),
                                           ModifiedBy   = @UserId
                                    WHERE  ItemId = @ItemId";

                                using (var cmd = new SqlCommand(decrementItemSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                                    cmd.Parameters.AddWithValue("@UserId",   userId);
                                    cmd.Parameters.AddWithValue("@ItemId",   sourceItemId.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // ── 4. Insert dbo.Inventory 'Negative' entry ──────────────
                                string invDescription =
                                    $"Non-refillable cartridge {action} by IT" +
                                    (rowReqId.HasValue ? $" - Req #{rowReqId.Value}" : string.Empty) +
                                    $" - EmptyCartridgeId {emptyCartridgeId}";

                                const string insertInventorySql = @"
                                    INSERT INTO dbo.Inventory
                                        (ItemId, EntryType, Quantity, DatePosted, PostedBy,
                                         ReqId, Description, ConditionID, Active)
                                    VALUES
                                        (@ItemId, 'Negative', @Quantity, GETDATE(), @PostedBy,
                                         @ReqId, @Description, @ConditionID, 1)";

                                using (var cmd = new SqlCommand(insertInventorySql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId",      sourceItemId.Value);
                                    cmd.Parameters.AddWithValue("@Quantity",    quantity);
                                    cmd.Parameters.AddWithValue("@PostedBy",    userId);
                                    cmd.Parameters.AddWithValue("@ReqId",       rowReqId.HasValue   ? (object)rowReqId.Value   : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Description", invDescription);
                                    cmd.Parameters.AddWithValue("@ConditionID", conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // ── 5. Insert dbo.CartridgeMovement 'Adjustment' entry ────
                                string movementRemarks =
                                    $"[{action}] Non-refillable empty cartridge" +
                                    (rowReqId.HasValue ? $" - Req #{rowReqId.Value}" : string.Empty) +
                                    $" - EmptyCartridgeId {emptyCartridgeId}";

                                const string insertMovementSql = @"
                                    INSERT INTO dbo.CartridgeMovement
                                        (ItemId, Quantity, MovementType, CartridgeTypeId,
                                         EmployeeId, CreatedBy, CreatedAt, Remarks)
                                    VALUES
                                        (@ItemId, @Quantity, 'Adjustment', @CartridgeTypeId,
                                         @UserId, @UserId, GETDATE(), @Remarks)";

                                using (var cmd = new SqlCommand(insertMovementSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId",          sourceItemId.Value);
                                    cmd.Parameters.AddWithValue("@Quantity",        quantity);
                                    cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                                    cmd.Parameters.AddWithValue("@UserId",          userId);
                                    cmd.Parameters.AddWithValue("@Remarks",         movementRemarks);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // ── [DEV] 5b. Lifecycle decision inserts (schema validation only) ─
                                // Writes dbo.ItemLifecycleDecision + dbo.ItemLifecycleDecisionCartridge
                                // inside the existing transaction.  Remove or promote during rollout.
                                string devDecisionTypeName = action == "Disposed" ? "DISPOSE" : "SELL";

                                const string devInsertLifecycleSql = @"
                                    DECLARE @DecisionTypeId INT =
                                        (SELECT DecisionTypeId
                                         FROM dbo.ItemDecisionType
                                         WHERE DecisionTypeName = @DecisionTypeName);

                                    IF NOT EXISTS (
                                        SELECT 1
                                        FROM dbo.ItemLifecycleDecision
                                        WHERE ItemId = @ItemId
                                          AND DecisionStatus = 'Executed'
                                    )
                                    BEGIN
                                        INSERT INTO dbo.ItemLifecycleDecision
                                            (ItemId, DecisionTypeId, ConditionId, Quantity,
                                             DecisionStatus, DecidedAt, DecidedBy, Remarks)
                                        VALUES (
                                            @ItemId,
                                            @DecisionTypeId,
                                            @ConditionId,
                                            @Quantity,
                                            'Executed',
                                            GETDATE(),
                                            @DecidedBy,
                                            @Remarks
                                        );
                                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                                    END
                                    ELSE
                                    BEGIN
                                        SELECT TOP 1 DecisionId
                                        FROM dbo.ItemLifecycleDecision
                                        WHERE ItemId = @ItemId
                                          AND DecisionStatus = 'Executed'
                                        ORDER BY DecidedAt DESC;
                                    END";

                                int devDecisionId;
                                using (var cmd = new SqlCommand(devInsertLifecycleSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId",           sourceItemId.Value);
                                    cmd.Parameters.AddWithValue("@DecisionTypeName", devDecisionTypeName);
                                    cmd.Parameters.AddWithValue("@ConditionId",      conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Quantity",         quantity);
                                    cmd.Parameters.AddWithValue("@DecidedBy",        userId);
                                    cmd.Parameters.AddWithValue("@Remarks",          movementRemarks);
                                    devDecisionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                }

                                const string devInsertLifecycleCartridgeSql = @"
                                    IF NOT EXISTS (
                                        SELECT 1
                                        FROM dbo.ItemLifecycleDecisionCartridge
                                        WHERE DecisionId = @DecisionId
                                    )
                                    INSERT INTO dbo.ItemLifecycleDecisionCartridge
                                        (DecisionId, EmptyCartridgeId)
                                    VALUES
                                        (@DecisionId, @EmptyCartridgeId);";

                                using (var cmd = new SqlCommand(devInsertLifecycleCartridgeSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@DecisionId",       devDecisionId);
                                    cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            // ── 6. Archive (idempotent) ───────────────────────────────────
                            const string insertArchiveSql = @"
                                IF NOT EXISTS (
                                    SELECT 1 FROM dbo.ArchiveStatus
                                    WHERE EntityType = 'EmptyCartridge' AND EntityId = @EntityId)
                                INSERT INTO dbo.ArchiveStatus
                                    (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES
                                    ('EmptyCartridge', @EntityId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@EntityId",      emptyCartridgeId);
                                cmd.Parameters.AddWithValue("@ArchivedBy",    userId.ToString());
                                cmd.Parameters.AddWithValue("@ArchiveReason", action);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        } // end foreach

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
        /// Marks a non-refillable empty cartridge as Disposed or Sold.
        /// Within a single transaction:
        ///   1. Validates the empty is still in Pending state and has a SourceItemId.
        ///   2. Updates EmptyCartridge.Status to <paramref name="action"/> ('Disposed' or 'Sold').
        ///   3. Decrements Item.StockOnHand by the empty's Quantity (delta-based pattern).
        ///   4. Inserts a dbo.Inventory row with EntryType = 'Negative'.
        ///   5. Inserts a dbo.CartridgeMovement row with MovementType = 'Adjustment'.
        /// </summary>
        private async Task MarkEmptyCartridgeActionAsync(int emptyCartridgeId, string action, int userId, string disposalCompanyName = null)
        {
            if (action != "Disposed" && action != "Sold")
                throw new ArgumentException($"Invalid action '{action}'. Must be 'Disposed' or 'Sold'.");

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // ── 1. Load the empty cartridge row ──────────────────────────────
                        int    quantity         = 0;
                        int?   sourceItemId     = null;
                        int?   reqId            = null;
                        int?   conditionId      = null;
                        int    cartridgeModelId = 0;
                        string currentStatus    = string.Empty;

                        const string selectSql = @"
                            SELECT ec.Quantity, ec.SourceItemId, ec.ReqId, ec.ConditionId, ec.Status,
                                   ec.CartridgeModelId
                            FROM   dbo.EmptyCartridge ec
                            WHERE  ec.EmptyCartridgeId = @EmptyCartridgeId";

                        using (var cmd = new SqlCommand(selectSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    throw new InvalidOperationException($"EmptyCartridgeId {emptyCartridgeId} not found.");

                                quantity         = reader.GetInt32(0);
                                sourceItemId     = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                                reqId            = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                                conditionId      = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                currentStatus    = reader.GetString(4);
                                cartridgeModelId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                            }
                        }

                        if (currentStatus == "Disposed" || currentStatus == "Sold")
                            throw new InvalidOperationException(
                                $"EmptyCartridgeId {emptyCartridgeId} is already '{currentStatus}'.");

                        // ── 1b. Fallback: locate IT custody Item when SourceItemId is missing ──
                        // Pre-fix records (returned before SourceItemId was introduced) have NULL.
                        // Try to find the IT custody Item by CartridgeModelId + custody marker.
                        if (sourceItemId == null && cartridgeModelId > 0)
                        {
                            const string fallbackSql = @"
                                SELECT TOP 1 ItemId
                                FROM   dbo.Item
                                WHERE  CartridgeModelId = @CartridgeModelId
                                  AND  Active           = 1
                                  AND  Category         = 'Cartridge'
                                  AND  RefillStatus     IS NULL
                                  AND  Remarks LIKE '%IT custody%'
                                ORDER  BY DateCreated DESC";

                            using (var cmd = new SqlCommand(fallbackSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                                var result = await cmd.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                    sourceItemId = Convert.ToInt32(result);
                            }
                        }

                        // ── 2. Update EmptyCartridge.Status (+ optional DisposalCompanyName) ──
                        const string updateEmptySql = @"
                            UPDATE dbo.EmptyCartridge
                            SET    Status              = @Action,
                                   DateModified        = GETDATE(),
                                   ModifiedBy          = @UserId,
                                   DisposalCompanyName = @DisposalCompanyName
                            WHERE  EmptyCartridgeId = @EmptyCartridgeId";

                        using (var cmd = new SqlCommand(updateEmptySql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Action",              action);
                            cmd.Parameters.AddWithValue("@UserId",              userId);
                            cmd.Parameters.AddWithValue("@EmptyCartridgeId",    emptyCartridgeId);
                            cmd.Parameters.AddWithValue("@DisposalCompanyName",
                                string.IsNullOrWhiteSpace(disposalCompanyName)
                                    ? (object)DBNull.Value
                                    : disposalCompanyName.Trim());
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ── 3-5. Inventory steps (only when an IT custody Item exists) ────
                        if (sourceItemId != null)
                        {
                            // ── 3. Decrement Item.StockOnHand ─────────────────────────────
                            const string decrementItemSql = @"
                                UPDATE dbo.Item
                                SET    StockOnHand  = StockOnHand - @Quantity,
                                       DateModified = GETDATE(),
                                       ModifiedBy   = @UserId
                                WHERE  ItemId = @ItemId";

                            using (var cmd = new SqlCommand(decrementItemSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Quantity", quantity);
                                cmd.Parameters.AddWithValue("@UserId",   userId);
                                cmd.Parameters.AddWithValue("@ItemId",   sourceItemId.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ── 4. Insert dbo.Inventory negative entry ────────────────────
                            string invDescription = $"Non-refillable cartridge {action} by IT" +
                                (reqId.HasValue ? $" - Req #{reqId.Value}" : string.Empty) +
                                $" - EmptyCartridgeId {emptyCartridgeId}";

                            const string insertInventorySql = @"
                                INSERT INTO dbo.Inventory
                                    (ItemId, EntryType, Quantity, DatePosted, PostedBy,
                                     ReqId, Description, ConditionID, Active)
                                VALUES
                                    (@ItemId, 'Negative', @Quantity, GETDATE(), @PostedBy,
                                     @ReqId, @Description, @ConditionID, 1)";

                            using (var cmd = new SqlCommand(insertInventorySql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId",      sourceItemId.Value);
                                cmd.Parameters.AddWithValue("@Quantity",    quantity);
                                cmd.Parameters.AddWithValue("@PostedBy",    userId);
                                cmd.Parameters.AddWithValue("@ReqId",       reqId.HasValue      ? (object)reqId.Value      : DBNull.Value);
                                cmd.Parameters.AddWithValue("@Description", invDescription);
                                cmd.Parameters.AddWithValue("@ConditionID", conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ── 5. Insert dbo.CartridgeMovement Adjustment entry ──────────
                            // CartridgeTypeId: look up 'Brand New'; fall back to first row if missing.
                            int cartridgeTypeId = 1;
                            const string cartridgeTypeSql = @"
                                SELECT TOP 1 CartridgeTypeId
                                FROM   dbo.CartridgeType
                                WHERE  CartridgeTypeName = 'Brand New'
                                ORDER  BY CartridgeTypeId ASC";

                            using (var cmd = new SqlCommand(cartridgeTypeSql, con, transaction))
                            {
                                var ctResult = await cmd.ExecuteScalarAsync();
                                if (ctResult != null && ctResult != DBNull.Value)
                                    cartridgeTypeId = Convert.ToInt32(ctResult);
                            }

                            string movementRemarks = $"[{action}] Non-refillable empty cartridge" +
                                (reqId.HasValue ? $" - Req #{reqId.Value}" : string.Empty) +
                                $" - EmptyCartridgeId {emptyCartridgeId}";

                            const string insertMovementSql = @"
                                INSERT INTO dbo.CartridgeMovement
                                    (ItemId, Quantity, MovementType, CartridgeTypeId,
                                     EmployeeId, CreatedBy, CreatedAt, Remarks)
                                VALUES
                                    (@ItemId, @Quantity, 'Adjustment', @CartridgeTypeId,
                                     @UserId, @UserId, GETDATE(), @Remarks)";

                            using (var cmd = new SqlCommand(insertMovementSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId",          sourceItemId.Value);
                                cmd.Parameters.AddWithValue("@Quantity",        quantity);
                                cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                                cmd.Parameters.AddWithValue("@UserId",          userId);
                                cmd.Parameters.AddWithValue("@Remarks",         movementRemarks);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ── [DEV] 5b. Lifecycle decision inserts (schema validation only) ─
                            // Writes dbo.ItemLifecycleDecision + dbo.ItemLifecycleDecisionCartridge
                            // inside the existing transaction.  Remove or promote during rollout.
                            string devDecisionTypeName = action == "Disposed" ? "DISPOSE" : "SELL";

                            const string devInsertLifecycleSql = @"
                                DECLARE @DecisionTypeId INT =
                                    (SELECT DecisionTypeId
                                     FROM dbo.ItemDecisionType
                                     WHERE DecisionTypeName = @DecisionTypeName);

                                IF NOT EXISTS (
                                    SELECT 1
                                    FROM dbo.ItemLifecycleDecision
                                    WHERE ItemId = @ItemId
                                      AND DecisionStatus = 'Executed'
                                )
                                BEGIN
                                    INSERT INTO dbo.ItemLifecycleDecision
                                        (ItemId, DecisionTypeId, ConditionId, Quantity,
                                         DecisionStatus, DecidedAt, DecidedBy, Remarks)
                                    VALUES (
                                        @ItemId,
                                        @DecisionTypeId,
                                        @ConditionId,
                                        @Quantity,
                                        'Executed',
                                        GETDATE(),
                                        @DecidedBy,
                                        @Remarks
                                    );
                                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                                END
                                ELSE
                                BEGIN
                                    SELECT TOP 1 DecisionId
                                    FROM dbo.ItemLifecycleDecision
                                    WHERE ItemId = @ItemId
                                      AND DecisionStatus = 'Executed'
                                    ORDER BY DecidedAt DESC;
                                END";

                            int devDecisionId;
                            using (var cmd = new SqlCommand(devInsertLifecycleSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId",           sourceItemId.Value);
                                cmd.Parameters.AddWithValue("@DecisionTypeName", devDecisionTypeName);
                                cmd.Parameters.AddWithValue("@ConditionId",      conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@Quantity",         quantity);
                                cmd.Parameters.AddWithValue("@DecidedBy",        userId);
                                cmd.Parameters.AddWithValue("@Remarks",          movementRemarks);
                                devDecisionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            const string devInsertLifecycleCartridgeSql = @"
                                IF NOT EXISTS (
                                    SELECT 1
                                    FROM dbo.ItemLifecycleDecisionCartridge
                                    WHERE DecisionId = @DecisionId
                                )
                                INSERT INTO dbo.ItemLifecycleDecisionCartridge
                                    (DecisionId, EmptyCartridgeId)
                                VALUES
                                    (@DecisionId, @EmptyCartridgeId);";

                            using (var cmd = new SqlCommand(devInsertLifecycleCartridgeSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@DecisionId",       devDecisionId);
                                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        // No IT custody Item found (pre-fix legacy record) — status update only.

                        // ── 6. Archive: insert into dbo.ArchiveStatus ────────────────────
                        // Idempotency: if a row for this entity already exists (shouldn't
                        // happen since we block re-actioning above), skip the INSERT.
                        const string insertArchiveSql = @"
                            IF NOT EXISTS (
                                SELECT 1 FROM dbo.ArchiveStatus
                                WHERE EntityType = 'EmptyCartridge' AND EntityId = @EntityId)
                            INSERT INTO dbo.ArchiveStatus
                                (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                            VALUES
                                ('EmptyCartridge', @EntityId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                        using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@EntityId",      emptyCartridgeId);
                            cmd.Parameters.AddWithValue("@ArchivedBy",    userId.ToString());
                            cmd.Parameters.AddWithValue("@ArchiveReason", action); // 'Disposed' or 'Sold'
                            await cmd.ExecuteNonQueryAsync();
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
        /// Links a returned cartridge to a vendor batch
        /// Updates the cartridge's BatchId field
        /// </summary>
        public async Task LinkCartridgeToBatchAsync(int itemId, int batchId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    UPDATE dbo.Cartridge
                    SET BatchId = @BatchId,
                        RefillStatus = 'For Refill'
                    WHERE ItemId = @ItemId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Updates the status of all empty cartridges in a batch
        /// Used when batch status changes (e.g., sent to vendor, received)
        /// ARCHITECTURAL: Updates EmptyCartridge (WIP inventory)
        /// Also syncs RefillStatus based on the batch lifecycle:
        ///   SentForRefill/SentToVendor → RefillStatus = 'Refilling'
        ///   Completed/Received → RefillStatus = 'Refilled'
        /// </summary>
        public async Task UpdateBatchCartridgeStatusAsync(int batchId, string status, int userId = 0)
        {
            // Map batch status to RefillStatus
            string refillStatus = null;
            if (status == "SentForRefill" || status == "SentToVendor" || status == "InRefill")
                refillStatus = "Refilling";
            else if (status == "Completed" || status == "Received")
                refillStatus = "Refilled";

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // CRITICAL FIX: Include ModifiedBy in the UPDATE
                // Previously ModifiedBy was never set, leaving it NULL
                var sql = refillStatus != null
                    ? @"UPDATE dbo.EmptyCartridge
                        SET Status = @Status,
                            RefillStatus = @RefillStatus,
                            DateModified = GETDATE(),
                            ModifiedBy = CASE WHEN @UserId > 0 THEN @UserId ELSE ModifiedBy END
                        WHERE VendorBatchId = @BatchId"
                    : @"UPDATE dbo.EmptyCartridge
                        SET Status = @Status,
                            DateModified = GETDATE(),
                            ModifiedBy = CASE WHEN @UserId > 0 THEN @UserId ELSE ModifiedBy END
                        WHERE VendorBatchId = @BatchId";

                int rowsAffected;
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    if (refillStatus != null)
                        cmd.Parameters.AddWithValue("@RefillStatus", refillStatus);
                    rowsAffected = await cmd.ExecuteNonQueryAsync();
                }

                if (rowsAffected == 0)
                    System.Diagnostics.Debug.WriteLine($"WARNING: UpdateBatchCartridgeStatusAsync updated 0 EmptyCartridge rows for BatchId={batchId}, Status={status}");
            }
        }

        /// <summary>
        /// Increments the ReturnedQty for a batch by the specified amount.
        /// 
        /// REFILL-VENDOR RULE: Only Active or ThresholdMet batches can accept returns.
        /// Closed, ForReturn, SentForRefill, and Completed batches are immutable.
        /// Late-returned cartridges must NOT be forced into old/closed batches;
        /// they should be routed to the NEXT active batch instead.
        /// </summary>
        /// <returns>Number of rows affected (0 if batch was not in an updatable status)</returns>
        public async Task<int> IncrementBatchReturnedQtyAsync(int batchId, int quantityReturned)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // Guard: only Active batches accumulate returns.
                // Sealed batches (ForReturn, SentForRefill, Completed, Closed) are immutable.
                var sql = @"
                    UPDATE dbo.VendorCartridgeBatch
                    SET ReturnedQty = ReturnedQty + @QuantityReturned
                    WHERE BatchId = @BatchId
                      AND Status = 'Active'";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                    cmd.Parameters.AddWithValue("@QuantityReturned", quantityReturned);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"WARNING: IncrementBatchReturnedQtyAsync updated 0 rows for BatchId={batchId}. " +
                            "Batch may be closed/completed — late returns should go to the next active batch.");
                    }

                    return rowsAffected;
                }
            }
        }

        public async Task<RefillTransactionDto> GetRefillTransactionByIdAsync(int refillTransactionId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        rt.RefillTransactionId,
                        rt.BatchId,
                        rt.VendorId,
                        v.VendorName,
                        rt.SentQty,
                        rt.SentDate,
                        rt.Status,
                        rt.CreatedBy,
                        rt.CreatedDate,
                        rt.Remarks
                    FROM dbo.RefillTransaction rt
                    INNER JOIN dbo.Vendor v ON rt.VendorId = v.VendorID
                    WHERE rt.RefillTransactionId = @RefillTransactionId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@RefillTransactionId", refillTransactionId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new RefillTransactionDto
                            {
                                RefillTransactionId = reader.GetInt32(0),
                                BatchId             = reader.GetInt32(1),
                                VendorId            = reader.GetInt32(2),
                                VendorName          = reader.GetString(3),
                                SentQty             = reader.GetInt32(4),
                                SentDate            = reader.GetDateTime(5),
                                Status              = reader.GetString(6),
                                CreatedBy           = reader.GetInt32(7),
                                CreatedDate         = reader.GetDateTime(8),
                                Remarks             = reader.IsDBNull(9) ? null : reader.GetString(9)
                            };
                        }
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Gets all refill transactions for a batch (lightweight outbound log).
        /// </summary>
        public async Task<List<RefillTransactionDto>> GetBatchTransactionsAsync(int batchId)
        {
            var transactions = new List<RefillTransactionDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        rt.RefillTransactionId,
                        rt.BatchId,
                        rt.VendorId,
                        v.VendorName,
                        rt.SentQty,
                        rt.SentDate,
                        rt.Status,
                        rt.CreatedBy,
                        rt.CreatedDate,
                        rt.Remarks
                    FROM dbo.RefillTransaction rt
                    INNER JOIN dbo.Vendor v ON rt.VendorId = v.VendorID
                    WHERE rt.BatchId = @BatchId
                    ORDER BY rt.SentDate DESC";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transactions.Add(new RefillTransactionDto
                            {
                                RefillTransactionId = reader.GetInt32(0),
                                BatchId             = reader.GetInt32(1),
                                VendorId            = reader.GetInt32(2),
                                VendorName          = reader.GetString(3),
                                SentQty             = reader.GetInt32(4),
                                SentDate            = reader.GetDateTime(5),
                                Status              = reader.GetString(6),
                                CreatedBy           = reader.GetInt32(7),
                                CreatedDate         = reader.GetDateTime(8),
                                Remarks             = reader.IsDBNull(9) ? null : reader.GetString(9)
                            });
                        }
                    }
                }
            }

            return transactions;
        }

        private async Task<bool> ColumnExistsAsync(SqlConnection con, string tableName, string columnName)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                  AND COLUMN_NAME = @ColumnName";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        public async Task<List<EmptyCartridgesByVendorDto>> GetEmptyCartridgesByVendorAsync()
        {
            var result = new List<EmptyCartridgesByVendorDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // Vendor is not known at the empty stage — group by CartridgeModelId only.
                // Show pending empties not yet assigned to any batch (or in an Active batch).
                const string sql = @"
                    SELECT
                        0                AS VendorId,
                        '[Unassigned]'   AS VendorName,
                        ec.CartridgeModelId,
                        ISNULL(cm.ModelNumber, '[DATA ERROR: Missing CartridgeModelId for EmptyCartridgeId ' + CAST(MIN(ec.EmptyCartridgeId) AS NVARCHAR) + ']') AS CartridgeModel,
                        SUM(ec.Quantity) AS EmptyQty
                    FROM dbo.EmptyCartridge ec
                    LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                    LEFT JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId = vcb.BatchId
                    WHERE ec.Status = 'Pending'
                      AND ISNULL(cm.IsRefillable, 1) = 1
                      AND (
                          ec.VendorBatchId IS NULL
                          OR ec.VendorBatchId = 0
                          OR vcb.Status = 'Active'
                      )
                    GROUP BY ec.CartridgeModelId, cm.ModelNumber
                    HAVING SUM(ec.Quantity) > 0
                    ORDER BY CASE WHEN cm.ModelNumber IS NULL THEN 0 ELSE 1 END,
                             cm.ModelNumber";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new EmptyCartridgesByVendorDto
                        {
                            VendorId         = 0,
                            VendorName       = "[Unassigned]",
                            CartridgeModelId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            CartridgeModel   = reader.GetString(3),
                            EmptyQty         = reader.GetInt32(4)
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieves audit trail for a specific vendor batch
        /// Shows request-level rows that contributed to the batch
        /// READ-ONLY query for audit trail display
        /// </summary>
        public async Task<List<VendorBatchAuditTrailDto>> GetBatchAuditTrailAsync(int batchId)
        {
            var result = new List<VendorBatchAuditTrailDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        ec.EmptyCartridgeId,
                        ec.ReqId,
                        ec.ReturnedAt,
                        ec.Quantity,
                        ISNULL(ec.CartridgeModelId, 0)            AS CartridgeModelId,
                        ISNULL(cm.ModelNumber, '[Missing Model]') AS CartridgeModel,
                        v.VendorName,
                        ec.VendorBatchId,
                        CASE
                            WHEN co.Name IS NOT NULL AND dp.Name IS NOT NULL AND br.Name IS NOT NULL
                                THEN co.Name + CHAR(13) + CHAR(10) + dp.Name + CHAR(13) + CHAR(10) + br.Name
                            WHEN co.Name IS NOT NULL AND br.Name IS NOT NULL
                                THEN co.Name + CHAR(13) + CHAR(10) + br.Name
                            WHEN br.Name IS NOT NULL
                                THEN br.Name
                            ELSE 'Unknown'
                        END AS ReturnedByName,
                        ec.Remarks
                    FROM dbo.EmptyCartridge ec
                    LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                    INNER JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId = vcb.BatchId
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    LEFT JOIN dbo.Branch br ON br.BranchId = ec.BranchId
                    OUTER APPLY (
                        SELECT TOP 1 bdc.CompanyID, bdc.DepartmentID
                        FROM   dbo.BranchDepartmentCompany bdc
                        WHERE  bdc.BranchID = ec.BranchId
                        ORDER BY bdc.BranchDeptCompanyID
                    ) bdc_br
                    LEFT JOIN dbo.Company co ON co.ComId = bdc_br.CompanyID
                    LEFT JOIN dbo.Department dp ON dp.DeptId = ISNULL(ec.DeptId, bdc_br.DepartmentID)
                    WHERE ec.VendorBatchId = @BatchId
                    ORDER BY ec.ReturnedAt DESC, ec.EmptyCartridgeId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@BatchId", batchId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new VendorBatchAuditTrailDto
                            {
                                EmptyCartridgeId = reader.GetInt32(0),
                                RequestId        = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                RequestDate      = reader.GetDateTime(2),
                                ReturnedQty      = reader.GetInt32(3),
                                CartridgeModelId = reader.GetInt32(4),
                                CartridgeModel   = reader.GetString(5),
                                Vendor           = reader.GetString(6),
                                BatchId          = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                                ReturnedByName   = reader.GetString(8),
                                Remarks          = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// No-op: threshold logic has been removed. Does nothing.
        /// </summary>
        public async Task UpdateBatchThresholdAsync(int batchId, decimal requiredReturnPercent)
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Creates a vendor batch and assigns empty cartridges to it.
        /// Marks assigned cartridges as BatchAssigned.
        /// 
        /// REFILL-VENDOR DESIGN:
        /// - VendorId = the REFILL vendor, NOT necessarily the original supplier.
        /// - A cartridge may be supplied by Vendor A but refilled by Vendor B.
        /// - The refill vendor is explicitly selected by IT when creating the batch.
        /// - OriginalQty is computed from historical CartridgeMovement records by
        ///   CartridgeModelId ONLY (not filtered by supplier VendorId).
        /// - This value is snapshotted at batch creation and NEVER recalculated.
        /// - RequiredQty = CEILING(OriginalQty * ExpectedReturnPercentage / 100)
        /// - Batch status becomes ThresholdMet only when ReturnedQty >= RequiredQty
        /// 
        /// LATE RETURNS:
        /// - Cartridges returned after a batch is closed are NOT forced into old batches.
        /// - They will be included in the NEXT active batch under whichever refill vendor
        ///   is assigned at that time.
        /// 
        /// SCHEMA: Uses CartridgeModelId (NOT text-based CartridgeModel)
        /// </summary>
        public async Task<int> CreateBatchAndAssignEmptiesAsync(int vendorId, int cartridgeModelId, int requiredQty, int createdBy, string remarks)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // PRE-CHECK: Verify CartridgeModelId exists and is active.
                        // REFILL-VENDOR FIX: Do NOT filter by VendorId here.
                        // The refill vendor may differ from the supplier that owns the model.
                        // We only need to confirm the model itself is valid.
                        const string validateModelSql = @"
                            SELECT ModelNumber
                            FROM dbo.CartridgeModel
                            WHERE CartridgeModelId = @CartridgeModelId
                              AND IsActive = 1";

                        string modelNumber = null;
                        using (var cmd = new SqlCommand(validateModelSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                modelNumber = result.ToString();
                            }
                        }

                        if (modelNumber == null)
                        {
                            throw new InvalidOperationException(
                                $"CartridgeModelId {cartridgeModelId} not found or inactive. " +
                                "Please ensure the cartridge model exists and is active.");
                        }

                        // REFILL-VENDOR FIX: Compute OriginalQty by CartridgeModelId only.
                        // The refill vendor may differ from the supplier, so we do NOT filter
                        // by VendorId. OriginalQty = total cartridges of this MODEL that ever
                        // entered inventory, regardless of which vendor supplied them.
                        const string computeOriginalQtySql = @"
                            SELECT ISNULL(SUM(cm.Quantity), 0)
                            FROM dbo.CartridgeMovement cm
                            INNER JOIN dbo.Item i ON cm.ItemId = i.ItemId
                            WHERE i.CartridgeModelId = @CartridgeModelId
                              AND cm.MovementType IN ('StockIn', 'RefillIn')";

                        int snapshotOriginalQty;
                        using (var cmd = new SqlCommand(computeOriginalQtySql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                            var result = await cmd.ExecuteScalarAsync();
                            snapshotOriginalQty = Convert.ToInt32(result);
                        }

                        // If no historical data, fall back to requiredQty (minimum 1)
                        if (snapshotOriginalQty <= 0)
                        {
                            snapshotOriginalQty = Math.Max(requiredQty, 1);
                        }

                        // Check if an Active batch already exists for this vendor + model.
                        // ONE batch per combination; reuse if found.
                        const string findExistingBatchSql = @"
                            SELECT BatchId
                            FROM dbo.VendorCartridgeBatch
                            WHERE VendorId = @VendorId
                              AND CartridgeModelId = @CartridgeModelId
                              AND Status = 'Active'";

                        int batchId = 0;
                        using (var cmd = new SqlCommand(findExistingBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@VendorId", vendorId);
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                batchId = Convert.ToInt32(result);
                            }
                        }

                        // If batch already exists, recalculate ReturnedQty from actual linked empties
                        // This fixes any discrepancies between ReturnedQty and actual EmptyCartridge count
                        if (batchId > 0)
                        {
                            // Recalculate ReturnedQty from sum of linked EmptyCartridge records
                            const string recalcReturnedQtySql = @"
                                UPDATE dbo.VendorCartridgeBatch
                                SET ReturnedQty = (
                                    SELECT ISNULL(SUM(ec.Quantity), 0)
                                    FROM dbo.EmptyCartridge ec
                                    WHERE ec.VendorBatchId = @BatchId
                                )
                                WHERE BatchId = @BatchId";

                            using (var cmd = new SqlCommand(recalcReturnedQtySql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BatchId", batchId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                            return batchId;
                        }

                        // Create new batch (no threshold — any quantity is valid)
                        if (batchId == 0)
                        {
                            const string insertBatchSql = @"
                                INSERT INTO dbo.VendorCartridgeBatch
                                    (VendorId, CartridgeModelId, OriginalQty, ReturnedQty,
                                     Status, DateReceived, CreatedBy, CreatedDate, Remarks)
                                VALUES
                                    (@VendorId, @CartridgeModelId, @OriginalQty, 0,
                                     'Active', GETDATE(), @CreatedBy, GETDATE(), @Remarks);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                            using (var cmd = new SqlCommand(insertBatchSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@VendorId",         vendorId);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);
                                cmd.Parameters.AddWithValue("@OriginalQty",      snapshotOriginalQty);
                                cmd.Parameters.AddWithValue("@CreatedBy",        createdBy);
                                cmd.Parameters.AddWithValue("@Remarks",          (object)remarks ?? DBNull.Value);

                                batchId = (int)await cmd.ExecuteScalarAsync();
                            }
                        }

                        // 2. Get empty cartridge IDs for this cartridge model.
                        // REFILL-VENDOR FIX: Filter by CartridgeModelId only, NOT by VendorId.
                        // EmptyCartridge.VendorId is the SUPPLIER vendor, but the refill vendor
                        // (VendorCartridgeBatch.VendorId) may be different. We collect all pending
                        // empties of this model regardless of their original supplier.
                        const string getEmptiesSql = @"
                            SELECT EmptyCartridgeId, Quantity
                            FROM dbo.EmptyCartridge
                            WHERE CartridgeModelId = @CartridgeModelId
                              AND Status = 'Pending'
                              AND (VendorBatchId IS NULL OR VendorBatchId = 0)
                            ORDER BY ReturnedAt ASC";

                        var emptyCartridges = new List<(int Id, int Qty)>();
                        int totalAvailable = 0;
                        using (var cmd = new SqlCommand(getEmptiesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CartridgeModelId", cartridgeModelId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int id = reader.GetInt32(0);
                                    int qty = reader.GetInt32(1);
                                    emptyCartridges.Add((id, qty));
                                    totalAvailable += qty;

                                    if (totalAvailable >= requiredQty)
                                        break;
                                }
                            }
                        }

                        // 3. Assign empties to batch and mark as BatchAssigned
                        if (emptyCartridges.Count > 0)
                        {
                            const string updateEmptiesSql = @"
                                UPDATE dbo.EmptyCartridge
                                SET Status = 'BatchAssigned',
                                    VendorBatchId = @BatchId,
                                    DateModified = GETDATE(),
                                    ModifiedBy = @CreatedBy
                                WHERE EmptyCartridgeId = @EmptyCartridgeId";

                            foreach (var (id, qty) in emptyCartridges)
                            {
                                using (var cmd = new SqlCommand(updateEmptiesSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@BatchId", batchId);
                                    cmd.Parameters.AddWithValue("@EmptyCartridgeId", id);
                                    cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // 4. CRITICAL FIX: Update batch ReturnedQty to reflect assigned empties
                        // Without this, ReturnedQty stays at 0 even though empties are linked to the batch
                        // This fixes the bug where exchanges create empties but batch shows ReturnedQty = 0
                        const string updateBatchReturnedQtySql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET ReturnedQty = ReturnedQty + @TotalQty
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(updateBatchReturnedQtySql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@TotalQty", totalAvailable);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return batchId;
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
        /// Helper method to check if a table exists
        /// </summary>
        private async Task<bool> TableExistsAsync(SqlConnection con, string tableName)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @TableName";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        /// <summary>
        /// Helper method to check if a table exists (with transaction support)
        /// </summary>
        private async Task<bool> TableExistsAsync(SqlConnection con, SqlTransaction transaction, string tableName)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @TableName";

            using (var cmd = new SqlCommand(sql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        /// <summary>
        /// Marks a batch as ready to be sent to vendor for refill
        /// Status transition: ThresholdMet → ForReturn
        /// NO inventory changes - purely administrative workflow step
        /// </summary>
        public async Task MarkBatchForReturnAsync(int batchId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate current status
                        const string validateSql = @"
                            SELECT Status
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId";

                        string currentStatus;
                        using (var cmd = new SqlCommand(validateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result == null)
                                throw new InvalidOperationException($"Batch {batchId} not found.");
                            currentStatus = result.ToString();
                        }

                        if (currentStatus != "Active" && currentStatus != "ThresholdMet")
                            throw new InvalidOperationException($"Batch {batchId} cannot be marked for return. Current status: {currentStatus}. Expected: Active.");

                        // Update batch status
                        const string updateSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET Status = 'ForReturn',
                                MarkedForReturnDate = GETDATE(),
                                MarkedForReturnBy = @UserId
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(updateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // CRITICAL FIX: Update EmptyCartridge rows to RefillStatus='Refilling'
                        // Without this, RefillStatus stays 'For Refill' and never transitions
                        const string updateEmptiesSql = @"
                            UPDATE dbo.EmptyCartridge
                            SET RefillStatus = 'Refilling',
                                DateModified = GETDATE(),
                                ModifiedBy = @UserId
                            WHERE VendorBatchId = @BatchId
                              AND RefillStatus = 'For Refill'";

                        int emptyRowsUpdated;
                        using (var cmd = new SqlCommand(updateEmptiesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            emptyRowsUpdated = await cmd.ExecuteNonQueryAsync();
                        }

                        if (emptyRowsUpdated == 0)
                            System.Diagnostics.Debug.WriteLine($"WARNING: MarkBatchForReturnAsync updated 0 EmptyCartridge rows for BatchId={batchId}");

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
        /// Returns all batches with Status = 'Active', ordered by vendor name then model.
        /// Used by the manual batch-assignment wizard "Use Existing Active Batch" mode.
        /// </summary>
        public async Task<List<VendorCartridgeBatchDto>> GetAllActiveBatchesAsync()
        {
            var results = new List<VendorCartridgeBatchDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        vcb.BatchId,
                        vcb.VendorId,
                        v.VendorName,
                        ISNULL(cm.ModelNumber, '[Unknown Model]') AS CartridgeModel,
                        vcb.CartridgeModelId,
                        vcb.OriginalQty,
                        vcb.ReturnedQty,
                        vcb.Status,
                        vcb.DateReceived,
                        vcb.CreatedBy,
                        vcb.CreatedDate,
                        ISNULL(vcb.Remarks, '') AS Remarks
                    FROM dbo.VendorCartridgeBatch vcb
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    LEFT JOIN dbo.CartridgeModel cm ON vcb.CartridgeModelId = cm.CartridgeModelId
                    WHERE vcb.Status = 'Active'
                      AND ISNULL(vcb.BatchPurpose, 'REFILL') = 'REFILL'
                    ORDER BY v.VendorName, ISNULL(cm.ModelNumber, '')";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new VendorCartridgeBatchDto
                        {
                            BatchId          = reader.GetInt32(0),
                            VendorId         = reader.GetInt32(1),
                            VendorName       = reader.GetString(2),
                            CartridgeModel   = reader.GetString(3),
                            CartridgeModelId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            OriginalQty      = reader.GetInt32(5),
                            ReturnedQty      = reader.GetInt32(6),
                            Status           = reader.GetString(7),
                            DateReceived     = reader.GetDateTime(8),
                            CreatedBy        = reader.GetInt32(9),
                            CreatedDate      = reader.GetDateTime(10),
                            Remarks          = reader.GetString(11)
                        });
                    }
                }
            }

            return results;
        }

        // =====================================================================
        // OUTBOUND BATCH METHODS (DISPOSE / SELL)
        // =====================================================================

        /// <summary>
        /// Returns unassigned empty cartridges that are candidates for a DISPOSE or SELL batch.
        /// When <paramref name="nonRefillableOnly"/> is true, only non-refillable cartridges are
        /// returned (damaged refillable cartridges are excluded).
        /// </summary>
        public async Task<List<UnassignedReturnDto>> GetUnassignedOutboundCartridgesAsync(bool nonRefillableOnly = false, bool damagedOnly = false)
        {
            var results = new List<UnassignedReturnDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // nonRefillableOnly  → Non-Refillable Empty Cartridges page: non-refillable models, any condition.
                // damagedOnly        → Damaged Empty Cartridges page: DAMAGED condition only, any refillability.
                // neither            → Outbound Batches page (new batch): all non-refillable OR damaged.
                string whereCondition = nonRefillableOnly
                    ? "ISNULL(cm.IsRefillable, 1) = 0"
                    : damagedOnly
                        ? "(ec.ConditionStatus = 'DAMAGED' OR c.ConditionName = 'Damaged')"
                        : "(ISNULL(cm.IsRefillable, 1) = 0 OR ec.ConditionStatus = 'DAMAGED' OR c.ConditionName = 'Damaged')";

                string sql = $@"
                    SELECT ec.EmptyCartridgeId, ec.CartridgeModelId,
                           ISNULL(cm.ModelNumber, '[Unknown]') AS CartridgeModel,
                           ec.VendorId AS SupplierId,
                           ISNULL(v.VendorName, '[Unknown]') AS SupplierName,
                           ec.Quantity, ec.ReturnedAt,
                           ISNULL(ec.Remarks, '') AS Remarks,
                           CASE WHEN ec.ConditionStatus = 'DAMAGED' OR c.ConditionName = 'Damaged'
                                THEN 'DAMAGED' ELSE 'GOOD' END AS ConditionStatus
                    FROM dbo.EmptyCartridge ec
                    LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                    LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
                    LEFT JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                    WHERE ec.VendorBatchId IS NULL
                      AND ec.Status = 'Pending'
                      AND {whereCondition}
                    ORDER BY ISNULL(cm.ModelNumber, ''), ec.ReturnedAt ASC";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new UnassignedReturnDto
                        {
                            EmptyCartridgeId = reader.GetInt32(0),
                            CartridgeModelId = reader.GetInt32(1),
                            CartridgeModel   = reader.GetString(2),
                            SupplierId       = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            SupplierName     = reader.GetString(4),
                            Quantity         = reader.GetInt32(5),
                            ReturnedAt       = reader.GetDateTime(6),
                            Remarks          = reader.GetString(7),
                            ConditionStatus  = reader.IsDBNull(8) ? null : reader.GetString(8)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all active DISPOSE and SELL batches for the OutboundBatchesPage.
        /// </summary>
        public async Task<List<OutboundBatchDto>> GetOutboundBatchesAsync()
        {
            var results = new List<OutboundBatchDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        vcb.BatchId,
                        vcb.BatchPurpose,
                        vcb.VendorId,
                        v.VendorName,
                        vcb.ReturnedQty,
                        vcb.Status,
                        vcb.CreatedDate,
                        ISNULL(vcb.Remarks, '') AS Remarks,
                        (
                            SELECT STRING_AGG(x.ModelQty, ', ')
                            FROM (
                                SELECT ISNULL(cm2.ModelNumber, '[Unknown]')
                                       + ' (' + CAST(SUM(ec2.Quantity) AS VARCHAR) + ')' AS ModelQty
                                FROM dbo.EmptyCartridge ec2
                                LEFT JOIN dbo.CartridgeModel cm2
                                       ON ec2.CartridgeModelId = cm2.CartridgeModelId
                                WHERE ec2.VendorBatchId = vcb.BatchId
                                GROUP BY ec2.CartridgeModelId, cm2.ModelNumber
                            ) x
                        ) AS ModelSummary
                    FROM dbo.VendorCartridgeBatch vcb
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    WHERE vcb.BatchPurpose IN ('DISPOSE', 'SELL')
                      AND vcb.Status = 'Active'
                    ORDER BY vcb.CreatedDate DESC";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new OutboundBatchDto
                        {
                            BatchId      = reader.GetInt32(0),
                            BatchPurpose = reader.GetString(1),
                            VendorId     = reader.GetInt32(2),
                            VendorName   = reader.GetString(3),
                            TotalQty     = reader.GetInt32(4),
                            Status       = reader.GetString(5),
                            CreatedDate  = reader.GetDateTime(6),
                            Remarks      = reader.GetString(7),
                            ModelSummary = reader.IsDBNull(8) ? "" : reader.GetString(8)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all finalized DISPOSE/SELL batches (Status = 'Disposed' or 'Sold'),
        /// ordered by most recently closed first.
        /// </summary>
        public async Task<List<OutboundBatchDto>> GetFinalizedOutboundBatchesAsync()
        {
            var results = new List<OutboundBatchDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        vcb.BatchId,
                        vcb.BatchPurpose,
                        vcb.VendorId,
                        v.VendorName,
                        vcb.ReturnedQty,
                        vcb.Status,
                        vcb.CreatedDate,
                        vcb.ClosedDate,
                        ISNULL(vcb.Remarks, '') AS Remarks,
                        (
                            SELECT STRING_AGG(x.ModelQty, ', ')
                            FROM (
                                SELECT ISNULL(cm2.ModelNumber, '[Unknown]')
                                       + ' (' + CAST(SUM(ec2.Quantity) AS VARCHAR) + ')' AS ModelQty
                                FROM dbo.EmptyCartridge ec2
                                LEFT JOIN dbo.CartridgeModel cm2
                                       ON ec2.CartridgeModelId = cm2.CartridgeModelId
                                WHERE ec2.VendorBatchId = vcb.BatchId
                                GROUP BY ec2.CartridgeModelId, cm2.ModelNumber
                            ) x
                        ) AS ModelSummary
                    FROM dbo.VendorCartridgeBatch vcb
                    INNER JOIN dbo.Vendor v ON vcb.VendorId = v.VendorID
                    WHERE vcb.BatchPurpose IN ('DISPOSE', 'SELL')
                      AND vcb.Status IN ('Disposed', 'Sold')
                    ORDER BY vcb.ClosedDate DESC";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new OutboundBatchDto
                        {
                            BatchId      = reader.GetInt32(0),
                            BatchPurpose = reader.GetString(1),
                            VendorId     = reader.GetInt32(2),
                            VendorName   = reader.GetString(3),
                            TotalQty     = reader.GetInt32(4),
                            Status       = reader.GetString(5),
                            CreatedDate  = reader.GetDateTime(6),
                            ClosedDate   = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                            Remarks      = reader.GetString(8),
                            ModelSummary = reader.IsDBNull(9) ? "" : reader.GetString(9)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Creates a new DISPOSE or SELL batch.
        /// Validates the vendor has the appropriate flag (IsDisposer or IsBuyer).
        /// Uses the same multi-model batch structure as refill batches.
        /// Returns the new BatchId.
        /// </summary>
        public async Task<int> CreateOutboundBatchAsync<T>(
            int vendorId,
            IEnumerable<T> modelGroups,
            string purpose,
            int createdBy,
            string remarks = null) where T : class
        {
            if (purpose != "DISPOSE" && purpose != "SELL")
                throw new ArgumentException($"Invalid purpose '{purpose}'. Must be 'DISPOSE' or 'SELL'.");

            var groups = modelGroups.ToList();
            if (groups.Count == 0)
                throw new ArgumentException("At least one model group is required.");

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate vendor has the correct outbound flag
                        string flagColumn = purpose == "DISPOSE" ? "IsDisposer" : "IsBuyer";
                        string validateSql = $@"
                            SELECT VendorName FROM dbo.Vendor
                            WHERE VendorID = @VendorId AND {flagColumn} = 1 AND IsActive = 1";

                        using (var cmd = new SqlCommand(validateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@VendorId", vendorId);
                            var vendorName = await cmd.ExecuteScalarAsync();
                            if (vendorName == null)
                                throw new InvalidOperationException(
                                    $"Vendor {vendorId} does not have the '{flagColumn}' flag set, or is inactive.");
                        }

                        var modelIdProp  = typeof(T).GetProperty("CartridgeModelId");
                        var totalQtyProp = typeof(T).GetProperty("TotalQty");

                        if (modelIdProp == null || totalQtyProp == null)
                            throw new InvalidOperationException(
                                "Model groups must have CartridgeModelId and TotalQty properties.");

                        int firstModelId    = (int)modelIdProp.GetValue(groups[0]);
                        int totalOriginalQty = groups.Sum(g => (int)totalQtyProp.GetValue(g));

                        const string insertBatchSql = @"
                            INSERT INTO dbo.VendorCartridgeBatch
                                (VendorId, CartridgeModelId, BatchPurpose, OriginalQty, ReturnedQty,
                                 Status, DateReceived, CreatedBy, CreatedDate, Remarks)
                            VALUES
                                (@VendorId, @CartridgeModelId, @BatchPurpose, @OriginalQty, 0,
                                 'Active', GETDATE(), @CreatedBy, GETDATE(), @Remarks);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newBatchId;
                        using (var cmd = new SqlCommand(insertBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@VendorId",         vendorId);
                            cmd.Parameters.AddWithValue("@CartridgeModelId",  firstModelId);
                            cmd.Parameters.AddWithValue("@BatchPurpose",      purpose);
                            cmd.Parameters.AddWithValue("@OriginalQty",       totalOriginalQty);
                            cmd.Parameters.AddWithValue("@CreatedBy",         createdBy);
                            cmd.Parameters.AddWithValue("@Remarks",           (object)remarks ?? DBNull.Value);
                            newBatchId = (int)await cmd.ExecuteScalarAsync();
                        }

                        const string insertLineSql = @"
                            IF OBJECT_ID('dbo.VendorCartridgeBatchLine', 'U') IS NOT NULL
                            BEGIN
                                IF NOT EXISTS (
                                    SELECT 1 FROM dbo.VendorCartridgeBatchLine
                                    WHERE BatchId = @BatchId AND CartridgeModelId = @CartridgeModelId)
                                INSERT INTO dbo.VendorCartridgeBatchLine
                                    (BatchId, CartridgeModelId, SentQty, ReturnedQty)
                                VALUES
                                    (@BatchId, @CartridgeModelId, @SentQty, 0)
                            END";

                        foreach (var group in groups)
                        {
                            int modelId = (int)modelIdProp.GetValue(group);
                            int qty     = (int)totalQtyProp.GetValue(group);

                            using (var cmd = new SqlCommand(insertLineSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BatchId",          newBatchId);
                                cmd.Parameters.AddWithValue("@CartridgeModelId", modelId);
                                cmd.Parameters.AddWithValue("@SentQty",          qty);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return newBatchId;
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
        /// Deletes an Active outbound batch and unassigns any cartridges linked to it,
        /// restoring them to Pending status so they can be reassigned.
        /// Only Active outbound batches (BatchPurpose = DISPOSE or SELL) may be deleted.
        /// All-or-nothing transaction.
        /// </summary>
        public async Task DeleteOutboundBatchAsync(int batchId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate: must be an Active outbound batch
                        const string validateSql = @"
                            SELECT BatchPurpose
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId AND Status = 'Active'
                              AND BatchPurpose IN ('DISPOSE', 'SELL')";

                        using (var cmd = new SqlCommand(validateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result == null)
                                throw new InvalidOperationException(
                                    $"Batch #{batchId} is not a deletable outbound batch. " +
                                    "Only Active DISPOSE or SELL batches can be deleted.");
                        }

                        // Unassign linked cartridges — restore to Pending so they can be reassigned
                        const string unassignSql = @"
                            UPDATE dbo.EmptyCartridge
                            SET VendorBatchId  = NULL,
                                Status         = 'Pending',
                                DateModified   = GETDATE(),
                                ModifiedBy     = @UserId
                            WHERE VendorBatchId = @BatchId";

                        using (var cmd = new SqlCommand(unassignSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId",  userId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Delete batch lines first (FK child rows)
                        const string deleteLinesSql = @"
                            DELETE FROM dbo.VendorCartridgeBatchLine
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(deleteLinesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Delete the batch header
                        const string deleteSql = @"
                            DELETE FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId AND Status = 'Active'";

                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            await cmd.ExecuteNonQueryAsync();
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
        /// Deletes a refill batch (any status except SentForRefill) and unassigns
        /// any EmptyCartridge rows linked to it, restoring them to Pending.
        /// All-or-nothing transaction.
        /// </summary>
        public async Task DeleteRefillBatchAsync(int batchId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate: must exist and not be SentForRefill (already with vendor)
                        const string validateSql = @"
                            SELECT Status
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId";

                        string status;
                        using (var cmd = new SqlCommand(validateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result == null)
                                throw new InvalidOperationException($"Batch #{batchId} not found.");
                            status = result.ToString();
                        }

                        if (status == "SentForRefill")
                            throw new InvalidOperationException(
                                $"Batch #{batchId} has already been sent to the vendor and cannot be deleted.");

                        // Unassign linked empty cartridges — restore to Pending
                        const string unassignSql = @"
                            UPDATE dbo.EmptyCartridge
                            SET VendorBatchId  = NULL,
                                Status         = 'Pending',
                                RefillStatus   = CASE
                                                    WHEN RefillStatus = 'BatchAssigned' THEN 'For Refill'
                                                    ELSE RefillStatus
                                                 END,
                                DateModified   = GETDATE(),
                                ModifiedBy     = @UserId
                            WHERE VendorBatchId = @BatchId";

                        using (var cmd = new SqlCommand(unassignSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId",  userId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Delete batch lines first (FK child rows)
                        const string deleteLinesSql = @"
                            DELETE FROM dbo.VendorCartridgeBatchLine
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(deleteLinesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Delete the batch header
                        const string deleteSql = @"
                            DELETE FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(deleteSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            await cmd.ExecuteNonQueryAsync();
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
        /// Finalizes an outbound batch: sets batch status to 'Disposed' or 'Sold' and
        /// applies the matching action to all linked EmptyCartridge rows.
        /// All-or-nothing transaction.
        /// </summary>
        public async Task FinalizeOutboundBatchAsync(int batchId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // 1. Load and validate the batch
                        string batchPurpose;
                        const string loadBatchSql = @"
                            SELECT BatchPurpose, Status
                            FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(loadBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    throw new InvalidOperationException($"Batch {batchId} not found.");

                                batchPurpose = reader.GetString(0);
                                string status = reader.GetString(1);

                                if (status != "Active")
                                    throw new InvalidOperationException(
                                        $"Batch {batchId} is '{status}' — only Active batches can be finalized.");

                                if (batchPurpose != "DISPOSE" && batchPurpose != "SELL")
                                    throw new InvalidOperationException(
                                        $"Batch {batchId} has purpose '{batchPurpose}' — only DISPOSE/SELL batches can be finalized here.");
                            }
                        }

                        string action = batchPurpose == "DISPOSE" ? "Disposed" : "Sold";

                        // 2. Resolve CartridgeTypeId once for movement records
                        int cartridgeTypeId = 1;
                        const string cartridgeTypeSql = @"
                            SELECT TOP 1 CartridgeTypeId FROM dbo.CartridgeType
                            WHERE CartridgeTypeName = 'Brand New' ORDER BY CartridgeTypeId ASC";

                        using (var cmd = new SqlCommand(cartridgeTypeSql, con, transaction))
                        {
                            var ctResult = await cmd.ExecuteScalarAsync();
                            if (ctResult != null && ctResult != DBNull.Value)
                                cartridgeTypeId = Convert.ToInt32(ctResult);
                        }

                        // 3. Update batch status
                        const string updateBatchSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET Status = @Action, ClosedDate = GETDATE(), ClosedBy = @UserId
                            WHERE BatchId = @BatchId";

                        using (var cmd = new SqlCommand(updateBatchSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Action",  action);
                            cmd.Parameters.AddWithValue("@UserId",  userId);
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 4. Collect all EmptyCartridgeIds linked to this batch
                        var emptyIds = new List<int>();
                        const string selectEmptiesSql = @"
                            SELECT EmptyCartridgeId FROM dbo.EmptyCartridge
                            WHERE VendorBatchId = @BatchId";

                        using (var cmd = new SqlCommand(selectEmptiesSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                    emptyIds.Add(reader.GetInt32(0));
                            }
                        }

                        if (emptyIds.Count == 0)
                            throw new InvalidOperationException(
                                $"Batch {batchId} has no linked empty cartridges. " +
                                "Nothing was finalized. Ensure cartridges were assigned to the batch before finalizing.");

                        // 5. Apply per-row outbound action to each linked cartridge
                        foreach (int emptyId in emptyIds)
                        {
                            await ApplyOutboundActionAsync(con, transaction, emptyId, action, userId, null, cartridgeTypeId);
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
        /// Applies the Disposed/Sold action to a single EmptyCartridge row within an existing transaction.
        /// Shared by FinalizeOutboundBatchAsync.
        /// Steps: update EC status → decrement Item → insert Inventory → insert CartridgeMovement
        ///        → insert ItemLifecycleDecision + Cartridge → insert ArchiveStatus.
        /// </summary>
        private async Task ApplyOutboundActionAsync(
            SqlConnection con, SqlTransaction transaction,
            int emptyCartridgeId, string action, int userId,
            string disposalCompanyName, int cartridgeTypeId)
        {
            // Load row data
            int    quantity         = 0;
            int?   sourceItemId     = null;
            int?   rowReqId         = null;
            int?   conditionId      = null;
            int    rowModelId       = 0;

            const string selectRowSql = @"
                SELECT ec.Quantity, ec.SourceItemId, ec.ReqId,
                       ec.ConditionId, ec.CartridgeModelId
                FROM   dbo.EmptyCartridge ec
                WHERE  ec.EmptyCartridgeId = @EmptyCartridgeId";

            using (var cmd = new SqlCommand(selectRowSql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return; // already removed or not found
                    quantity     = reader.GetInt32(0);
                    sourceItemId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                    rowReqId     = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                    conditionId  = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                    rowModelId   = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                }
            }

            // Fallback: locate IT-custody item when SourceItemId is missing
            if (sourceItemId == null && rowModelId > 0)
            {
                const string fallbackSql = @"
                    SELECT TOP 1 ItemId FROM dbo.Item
                    WHERE  CartridgeModelId = @CartridgeModelId AND Active = 1
                      AND  Category = 'Cartridge' AND RefillStatus IS NULL
                      AND  Remarks LIKE '%IT custody%'
                    ORDER  BY DateCreated DESC";

                using (var cmd = new SqlCommand(fallbackSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@CartridgeModelId", rowModelId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        sourceItemId = Convert.ToInt32(result);
                }
            }

            // Update EmptyCartridge status
            const string updateEmptySql = @"
                UPDATE dbo.EmptyCartridge
                SET    Status              = @Action,
                       DateModified        = GETDATE(),
                       ModifiedBy          = @UserId,
                       DisposalCompanyName = @DisposalCompanyName
                WHERE  EmptyCartridgeId = @EmptyCartridgeId";

            using (var cmd = new SqlCommand(updateEmptySql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@Action",           action);
                cmd.Parameters.AddWithValue("@UserId",           userId);
                cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                cmd.Parameters.AddWithValue("@DisposalCompanyName",
                    string.IsNullOrWhiteSpace(disposalCompanyName)
                        ? (object)DBNull.Value : disposalCompanyName.Trim());
                await cmd.ExecuteNonQueryAsync();
            }

            if (sourceItemId != null)
            {
                // Decrement Item.StockOnHand
                const string decrementItemSql = @"
                    UPDATE dbo.Item
                    SET    StockOnHand  = StockOnHand - @Quantity,
                           DateModified = GETDATE(), ModifiedBy = @UserId
                    WHERE  ItemId = @ItemId";

                using (var cmd = new SqlCommand(decrementItemSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.Parameters.AddWithValue("@UserId",   userId);
                    cmd.Parameters.AddWithValue("@ItemId",   sourceItemId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert Inventory 'Negative' entry
                string invDescription =
                    $"Non-refillable cartridge {action} by IT" +
                    (rowReqId.HasValue ? $" - Req #{rowReqId.Value}" : string.Empty) +
                    $" - EmptyCartridgeId {emptyCartridgeId}";

                const string insertInventorySql = @"
                    INSERT INTO dbo.Inventory
                        (ItemId, EntryType, Quantity, DatePosted, PostedBy,
                         ReqId, Description, ConditionID, Active)
                    VALUES
                        (@ItemId, 'Negative', @Quantity, GETDATE(), @PostedBy,
                         @ReqId, @Description, @ConditionID, 1)";

                using (var cmd = new SqlCommand(insertInventorySql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ItemId",      sourceItemId.Value);
                    cmd.Parameters.AddWithValue("@Quantity",    quantity);
                    cmd.Parameters.AddWithValue("@PostedBy",    userId);
                    cmd.Parameters.AddWithValue("@ReqId",       rowReqId.HasValue   ? (object)rowReqId.Value   : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", invDescription);
                    cmd.Parameters.AddWithValue("@ConditionID", conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert CartridgeMovement 'Adjustment' entry
                string movementRemarks =
                    $"[{action}] Non-refillable empty cartridge" +
                    (rowReqId.HasValue ? $" - Req #{rowReqId.Value}" : string.Empty) +
                    $" - EmptyCartridgeId {emptyCartridgeId}";

                const string insertMovementSql = @"
                    INSERT INTO dbo.CartridgeMovement
                        (ItemId, Quantity, MovementType, CartridgeTypeId,
                         EmployeeId, CreatedBy, CreatedAt, Remarks)
                    VALUES
                        (@ItemId, @Quantity, 'Adjustment', @CartridgeTypeId,
                         @UserId, @UserId, GETDATE(), @Remarks)";

                using (var cmd = new SqlCommand(insertMovementSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ItemId",          sourceItemId.Value);
                    cmd.Parameters.AddWithValue("@Quantity",        quantity);
                    cmd.Parameters.AddWithValue("@CartridgeTypeId", cartridgeTypeId);
                    cmd.Parameters.AddWithValue("@UserId",          userId);
                    cmd.Parameters.AddWithValue("@Remarks",         movementRemarks);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert ItemLifecycleDecision + ItemLifecycleDecisionCartridge
                string decisionTypeName = action == "Disposed" ? "DISPOSE" : "SELL";

                const string insertLifecycleSql = @"
                    DECLARE @DecisionTypeId INT =
                        (SELECT DecisionTypeId FROM dbo.ItemDecisionType
                         WHERE DecisionTypeName = @DecisionTypeName);

                    IF NOT EXISTS (
                        SELECT 1 FROM dbo.ItemLifecycleDecision
                        WHERE ItemId = @ItemId AND DecisionStatus = 'Executed')
                    BEGIN
                        INSERT INTO dbo.ItemLifecycleDecision
                            (ItemId, DecisionTypeId, ConditionId, Quantity,
                             DecisionStatus, DecidedAt, DecidedBy, Remarks)
                        VALUES (
                            @ItemId, @DecisionTypeId, @ConditionId, @Quantity,
                            'Executed', GETDATE(), @DecidedBy, @Remarks);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                    END
                    ELSE
                    BEGIN
                        SELECT TOP 1 DecisionId FROM dbo.ItemLifecycleDecision
                        WHERE ItemId = @ItemId AND DecisionStatus = 'Executed'
                        ORDER BY DecidedAt DESC;
                    END";

                int decisionId;
                using (var cmd = new SqlCommand(insertLifecycleSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@ItemId",           sourceItemId.Value);
                    cmd.Parameters.AddWithValue("@DecisionTypeName", decisionTypeName);
                    cmd.Parameters.AddWithValue("@ConditionId",      conditionId.HasValue ? (object)conditionId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Quantity",         quantity);
                    cmd.Parameters.AddWithValue("@DecidedBy",        userId);
                    cmd.Parameters.AddWithValue("@Remarks",          movementRemarks);
                    decisionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                const string insertLifecycleCartridgeSql = @"
                    IF NOT EXISTS (
                        SELECT 1 FROM dbo.ItemLifecycleDecisionCartridge
                        WHERE DecisionId = @DecisionId)
                    INSERT INTO dbo.ItemLifecycleDecisionCartridge
                        (DecisionId, EmptyCartridgeId)
                    VALUES (@DecisionId, @EmptyCartridgeId);";

                using (var cmd = new SqlCommand(insertLifecycleCartridgeSql, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@DecisionId",       decisionId);
                    cmd.Parameters.AddWithValue("@EmptyCartridgeId", emptyCartridgeId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Archive (idempotent)
            const string insertArchiveSql = @"
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.ArchiveStatus
                    WHERE EntityType = 'EmptyCartridge' AND EntityId = @EntityId)
                INSERT INTO dbo.ArchiveStatus
                    (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES
                    ('EmptyCartridge', @EntityId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
            {
                cmd.Parameters.AddWithValue("@EntityId",      emptyCartridgeId);
                cmd.Parameters.AddWithValue("@ArchivedBy",    userId.ToString());
                cmd.Parameters.AddWithValue("@ArchiveReason", action);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BATCH TRANSFER / REMOVE OPERATIONS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes EmptyCartridge row(s) from an Active refill batch and returns them
        /// to the unassigned pool (Status = 'Pending', VendorBatchId = NULL).
        ///
        /// Key resolution:
        ///   - If reqId is provided: targets all rows WHERE ReqId = reqId
        ///     AND CartridgeModelId = cartridgeModelId AND VendorBatchId = batchId.
        ///   - If reqId is null: targets only the specific fallbackEmptyCartridgeId row.
        ///
        /// Decrements VendorCartridgeBatch.ReturnedQty by the total removed quantity.
        /// Returns the total quantity removed.
        /// </summary>
        public async Task<int> RemoveFromBatchAsync(
            int batchId, int? reqId, int cartridgeModelId, int fallbackEmptyCartridgeId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Validate batch is an Active refill batch
                        const string validateSql = @"
                            SELECT BatchId FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId
                              AND Status  = 'Active'
                              AND ISNULL(BatchPurpose, 'REFILL') = 'REFILL'";
                        using (var cmd = new SqlCommand(validateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            if (await cmd.ExecuteScalarAsync() == null)
                                throw new InvalidOperationException(
                                    $"Batch {batchId} is not an Active refill batch and cannot be modified.");
                        }

                        // Sum quantity of rows to be removed
                        string sumSql = reqId.HasValue
                            ? @"SELECT ISNULL(SUM(Quantity), 0) FROM dbo.EmptyCartridge
                                WHERE ReqId = @ReqId AND CartridgeModelId = @ModelId
                                  AND VendorBatchId = @BatchId AND Status = 'BatchAssigned'"
                            : @"SELECT ISNULL(SUM(Quantity), 0) FROM dbo.EmptyCartridge
                                WHERE EmptyCartridgeId = @FallbackId
                                  AND VendorBatchId = @BatchId AND Status = 'BatchAssigned'";

                        int totalQty;
                        using (var cmd = new SqlCommand(sumSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            if (reqId.HasValue)
                            {
                                cmd.Parameters.AddWithValue("@ReqId",   reqId.Value);
                                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                            }
                            else
                                cmd.Parameters.AddWithValue("@FallbackId", fallbackEmptyCartridgeId);

                            totalQty = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        if (totalQty <= 0)
                            throw new InvalidOperationException(
                                "No matching cartridges found in this batch to remove.");

                        // Return rows to the unassigned pool
                        string updateSql = reqId.HasValue
                            ? @"UPDATE dbo.EmptyCartridge
                                SET VendorBatchId = NULL, Status = 'Pending',
                                    DateModified  = GETDATE(), ModifiedBy = @UserId
                                WHERE ReqId = @ReqId AND CartridgeModelId = @ModelId
                                  AND VendorBatchId = @BatchId AND Status = 'BatchAssigned'"
                            : @"UPDATE dbo.EmptyCartridge
                                SET VendorBatchId = NULL, Status = 'Pending',
                                    DateModified  = GETDATE(), ModifiedBy = @UserId
                                WHERE EmptyCartridgeId = @FallbackId
                                  AND VendorBatchId = @BatchId AND Status = 'BatchAssigned'";

                        using (var cmd = new SqlCommand(updateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@UserId",  userId);
                            if (reqId.HasValue)
                            {
                                cmd.Parameters.AddWithValue("@ReqId",   reqId.Value);
                                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                            }
                            else
                                cmd.Parameters.AddWithValue("@FallbackId", fallbackEmptyCartridgeId);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Decrement batch ReturnedQty (floor at 0 to avoid negative values)
                        const string decrementSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET ReturnedQty = CASE WHEN ReturnedQty >= @Qty THEN ReturnedQty - @Qty ELSE 0 END
                            WHERE BatchId = @BatchId";
                        using (var cmd = new SqlCommand(decrementSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", batchId);
                            cmd.Parameters.AddWithValue("@Qty",     totalQty);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return totalQty;
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
        /// Transfers EmptyCartridge row(s) from one Active refill batch to another Active refill batch.
        ///
        /// Key resolution follows the same rules as RemoveFromBatchAsync.
        /// Decrements fromBatch.ReturnedQty and increments toBatch.ReturnedQty atomically.
        /// Returns the total quantity transferred.
        /// </summary>
        public async Task<int> TransferToBatchAsync(
            int fromBatchId, int toBatchId, int? reqId, int cartridgeModelId, int fallbackEmptyCartridgeId, int userId)
        {
            if (fromBatchId == toBatchId)
                throw new ArgumentException("Source and target batch must be different.");

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        const string validateActiveSql = @"
                            SELECT BatchId FROM dbo.VendorCartridgeBatch
                            WHERE BatchId = @BatchId
                              AND Status  = 'Active'
                              AND ISNULL(BatchPurpose, 'REFILL') = 'REFILL'";

                        // Validate source
                        using (var cmd = new SqlCommand(validateActiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", fromBatchId);
                            if (await cmd.ExecuteScalarAsync() == null)
                                throw new InvalidOperationException(
                                    $"Source batch {fromBatchId} is not an Active refill batch.");
                        }

                        // Validate target
                        using (var cmd = new SqlCommand(validateActiveSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", toBatchId);
                            if (await cmd.ExecuteScalarAsync() == null)
                                throw new InvalidOperationException(
                                    $"Target batch {toBatchId} is not an Active refill batch. " +
                                    "Only Active refill batches can receive transferred cartridges.");
                        }

                        // Sum quantity to transfer
                        string sumSql = reqId.HasValue
                            ? @"SELECT ISNULL(SUM(Quantity), 0) FROM dbo.EmptyCartridge
                                WHERE ReqId = @ReqId AND CartridgeModelId = @ModelId
                                  AND VendorBatchId = @FromBatchId AND Status = 'BatchAssigned'"
                            : @"SELECT ISNULL(SUM(Quantity), 0) FROM dbo.EmptyCartridge
                                WHERE EmptyCartridgeId = @FallbackId
                                  AND VendorBatchId = @FromBatchId AND Status = 'BatchAssigned'";

                        int totalQty;
                        using (var cmd = new SqlCommand(sumSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@FromBatchId", fromBatchId);
                            if (reqId.HasValue)
                            {
                                cmd.Parameters.AddWithValue("@ReqId",   reqId.Value);
                                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                            }
                            else
                                cmd.Parameters.AddWithValue("@FallbackId", fallbackEmptyCartridgeId);

                            totalQty = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        if (totalQty <= 0)
                            throw new InvalidOperationException(
                                "No matching cartridges found in the source batch to transfer.");

                        // Move to target batch (keep Status = 'BatchAssigned')
                        string updateSql = reqId.HasValue
                            ? @"UPDATE dbo.EmptyCartridge
                                SET VendorBatchId = @ToBatchId,
                                    DateModified  = GETDATE(), ModifiedBy = @UserId
                                WHERE ReqId = @ReqId AND CartridgeModelId = @ModelId
                                  AND VendorBatchId = @FromBatchId AND Status = 'BatchAssigned'"
                            : @"UPDATE dbo.EmptyCartridge
                                SET VendorBatchId = @ToBatchId,
                                    DateModified  = GETDATE(), ModifiedBy = @UserId
                                WHERE EmptyCartridgeId = @FallbackId
                                  AND VendorBatchId = @FromBatchId AND Status = 'BatchAssigned'";

                        using (var cmd = new SqlCommand(updateSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ToBatchId",   toBatchId);
                            cmd.Parameters.AddWithValue("@FromBatchId", fromBatchId);
                            cmd.Parameters.AddWithValue("@UserId",      userId);
                            if (reqId.HasValue)
                            {
                                cmd.Parameters.AddWithValue("@ReqId",   reqId.Value);
                                cmd.Parameters.AddWithValue("@ModelId", cartridgeModelId);
                            }
                            else
                                cmd.Parameters.AddWithValue("@FallbackId", fallbackEmptyCartridgeId);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Decrement source ReturnedQty
                        const string decrementSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET ReturnedQty = CASE WHEN ReturnedQty >= @Qty THEN ReturnedQty - @Qty ELSE 0 END
                            WHERE BatchId = @BatchId";
                        using (var cmd = new SqlCommand(decrementSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", fromBatchId);
                            cmd.Parameters.AddWithValue("@Qty",     totalQty);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Increment target ReturnedQty
                        const string incrementSql = @"
                            UPDATE dbo.VendorCartridgeBatch
                            SET ReturnedQty = ReturnedQty + @Qty
                            WHERE BatchId = @BatchId AND Status = 'Active'";
                        using (var cmd = new SqlCommand(incrementSql, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@BatchId", toBatchId);
                            cmd.Parameters.AddWithValue("@Qty",     totalQty);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return totalQty;
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
