using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Service layer for cartridge refill operations
    /// Contains NO UI logic and does NOT hardcode refill percentages
    /// </summary>
    public class CartridgeRefillService
    {
        private readonly CartridgeRefillRepository _repository;

        public CartridgeRefillService()
        {
            _repository = new CartridgeRefillRepository();
        }

        /// <summary>
        /// Gets refill eligibility for all vendor batches
        /// </summary>
        public async Task<List<RefillEligibilityResult>> GetRefillEligibilityAsync()
        {
            return await _repository.GetRefillEligibilityAsync();
        }

        /// <summary>
        /// Updates eligible batches to 'ThresholdMet' status
        /// Returns the number of batches updated
        /// </summary>
        public async Task<int> ProcessEligibleBatchesAsync()
        {
            return await _repository.UpdateEligibleBatchStatusAsync();
        }

        /// <summary>
        /// Gets a specific batch by ID
        /// </summary>
        public async Task<VendorCartridgeBatchDto> GetBatchByIdAsync(int batchId)
        {
            return await _repository.GetBatchByIdAsync(batchId);
        }

        /// <summary>
        /// Closes a refill batch that has been sent to vendor.
        /// Status: SentForRefill → Closed
        /// Also marks all linked empty cartridges as 'Closed' (terminal state).
        /// No inventory changes — refilled cartridges enter via BatchAddItemDialog.
        /// </summary>
        public async Task CloseRefillBatchAsync(int batchId, int userId)
        {
            await _repository.CloseRefillBatchAsync(batchId, userId);
        }

        /// <summary>
        /// Marks a batch as sent for refill and creates a refill transaction
        /// Also updates all cartridges in the batch to "SentForRefill" status
        /// </summary>
        public async Task<int> SendBatchForRefillAsync(int batchId, int vendorId, int sentQty, int createdBy, string remarks)
        {
            // Guard: reject non-refillable models before any state change
            await _repository.AssertBatchCartridgeModelIsRefillableAsync(batchId);

            // Update batch status to SentForRefill
            await _repository.UpdateBatchStatusAsync(batchId, "SentForRefill");

            // Update all empty cartridges in this batch to "SentForRefill" status
            await _repository.UpdateBatchCartridgeStatusAsync(batchId, "SentForRefill", createdBy);

            // Create refill transaction
            var transaction = new RefillTransactionDto
            {
                BatchId = batchId,
                VendorId = vendorId,
                SentQty = sentQty,
                SentDate = System.DateTime.Now,
                Status = "Sent",
                CreatedBy = createdBy,
                Remarks = remarks
            };

            return await _repository.CreateRefillTransactionAsync(transaction);
        }

        /// <summary>
        /// Computes the historical OriginalQty for a cartridge model.
        /// 
        /// REFILL-VENDOR DECOUPLING:
        /// OriginalQty is computed by CartridgeModelId ONLY — it counts all cartridges
        /// of this model that ever entered inventory, regardless of which vendor supplied them.
        /// The vendorId parameter is retained for backward compatibility but is NOT used in the query.
        /// 
        /// SOURCE OF TRUTH: dbo.CartridgeMovement table
        /// INCLUDED MOVEMENTS: StockIn, RefillIn (cartridges entering inventory)
        /// EXCLUDED MOVEMENTS: Issued, Returned, Adjustment (not new stock)
        /// </summary>
        public async Task<int> ComputeOriginalQtyFromHistoryAsync(int vendorId, int cartridgeModelId)
        {
            return await _repository.ComputeOriginalQtyFromHistoryAsync(vendorId, cartridgeModelId);
        }

        // =====================================================================
        // THREE CLEARLY SEPARATED BATCH OPERATIONS (service layer)
        //
        // 1. GetActiveBatchAsync        — lookup only
        // 2. CreateRefillBatchAsync     — explicit creation, admin / UI action only
        // 3. AssignReturnsToBatchAsync  — manual cross-vendor assignment
        //
        // Return processing (CartridgeManagementRepository) uses
        // TryGetEligibleBatchForReturn and NEVER calls any of the methods below.
        // =====================================================================

        /// <summary>
        /// Looks up the single ACTIVE refill batch for a vendor + cartridge model.
        /// Pure lookup — no batch is created.
        ///
        /// Returns BatchId &gt; 0, or 0 if no active batch exists.
        /// </summary>
        public async Task<int> GetActiveBatchAsync(int vendorId, int cartridgeModelId)
        {
            return await _repository.GetActiveBatchAsync(vendorId, cartridgeModelId);
        }

        /// <summary>
        /// Creates a new refill batch for the specified REFILL vendor and cartridge model.
        ///
        /// MUST be called only from an explicit user/admin action — never from
        /// cartridge-return processing.  Batch creation is a procurement decision:
        /// it selects a vendor, sets a return quota, and starts a vendor engagement.
        /// Allowing a return event to trigger this silently bypasses those controls.
        ///
        /// REFILL-VENDOR: VendorId = the refill vendor, NOT the original supplier.
        /// Any vendor may be assigned a refill batch for any cartridge model.
        ///
        /// Throws InvalidOperationException if an Active batch already exists.
        /// Call GetActiveBatchAsync first to check.
        /// </summary>
        public async Task<int> CreateRefillBatchAsync(int vendorId, int cartridgeModelId, int originalQty, int createdBy, string remarks = null)
        {
            return await _repository.CreateRefillBatchAsync(vendorId, cartridgeModelId, originalQty, createdBy, remarks);
        }

        /// <summary>
        /// Creates a multi-model refill batch for a single vendor.
        /// 
        /// Real-world shipment behavior: Multiple cartridge models are boxed together
        /// and shipped as one batch to a single refiller, not separated per model.
        /// 
        /// Creates:
        ///   - One VendorCartridgeBatch header (with first model's ID for backward compatibility)
        ///   - One VendorCartridgeBatchLine per model with quantities
        /// 
        /// Returns the newly created BatchId.
        /// </summary>
        public async Task<int> CreateMultiModelRefillBatchAsync<T>(
            int vendorId,
            IEnumerable<T> modelGroups,
            int createdBy,
            string remarks = null) where T : class
        {
            return await _repository.CreateMultiModelRefillBatchAsync(vendorId, modelGroups, createdBy, remarks);
        }

        /// <summary>
        /// Manually assigns a set of unassigned returned cartridges to a refill batch.
        ///
        /// CROSS-VENDOR by design: the original supplier recorded on each EmptyCartridge
        /// row is irrelevant here.  Any returned cartridge of the correct model can be
        /// assigned to any refill batch, regardless of who originally supplied it.
        ///
        /// Only unassigned rows (VendorBatchId IS NULL) are updated — existing
        /// assignments are never overwritten.
        ///
        /// Returns the total quantity (units) successfully assigned.
        /// </summary>
        public async Task<int> AssignReturnsToBatchAsync(int batchId, IEnumerable<int> emptyCartridgeIds, int userId)
        {
            return await _repository.AssignReturnsToBatchAsync(batchId, emptyCartridgeIds, userId);
        }

        /// <summary>
        /// Links a returned cartridge to a vendor batch
        /// Sets the cartridge's RefillStatus to "For Refill"
        /// </summary>
        public async Task LinkCartridgeToBatchAsync(int itemId, int batchId)
        {
            await _repository.LinkCartridgeToBatchAsync(itemId, batchId);
        }


        /// <summary>
        /// Gets all transactions for a batch
        /// </summary>
        public async Task<List<RefillTransactionDto>> GetBatchTransactionsAsync(int batchId)
        {
            return await _repository.GetBatchTransactionsAsync(batchId);
        }

        /// <summary>
        /// Increments the returned quantity for a batch.
        /// Only Active batches accept returns; sealed batches (ForReturn, SentForRefill, etc.) are immutable.
        /// </summary>
        /// <returns>Number of rows affected (0 if batch was not Active)</returns>
        public async Task<int> IncrementBatchReturnedQtyAsync(int batchId, int quantityReturned)
        {
            return await _repository.IncrementBatchReturnedQtyAsync(batchId, quantityReturned);
        }


        /// <summary>
        /// Returns all GOOD (or unclassified legacy) empty cartridges that have not yet been
        /// assigned to a refill batch.  Damaged returns are excluded — they must never
        /// appear in the refill-batch assignment wizard.
        /// Used by the manual batch-assignment wizard.
        /// </summary>
        public async Task<List<UnassignedReturnDto>> GetUnassignedReturnsAsync()
        {
            return await _repository.GetUnassignedReturnsAsync();
        }

        public async Task<List<int>> GetEligibleRefillEmptyCartridgeIdsAsync(IEnumerable<int> emptyCartridgeIds)
        {
            return await _repository.GetEligibleRefillEmptyCartridgeIdsAsync(emptyCartridgeIds);
        }

        /// <summary>
        /// Returns all DAMAGED empty cartridges that have not yet been resolved.
        /// These are intentionally excluded from the refill-batch wizard.
        /// Used by the Damaged Empty Cartridges page.
        /// </summary>
        public async Task<List<UnassignedReturnDto>> GetDamagedUnassignedReturnsAsync()
        {
            return await _repository.GetDamagedUnassignedReturnsAsync();
        }

        /// <summary>
        /// Returns all EmptyCartridge records whose CartridgeModel has IsRefillable == false.
        /// These are under IT custody and must be disposed of or sold internally.
        /// </summary>
        public async Task<List<NonRefillableEmptyDto>> GetNonRefillableEmptiesAsync()
        {
            return await _repository.GetNonRefillableEmptiesAsync();
        }

        // MarkAsDisposedAsync and MarkAsSoldAsync removed — use outbound batch workflow instead.

        /// <summary>
        /// Returns all non-refillable empty cartridges that have been Sold, ordered newest first.
        /// Read-only — for the Sold Cartridges audit page.
        /// </summary>
        public async Task<List<ClosedEmptyCartridgeDto>> GetSoldEmptiesAsync()
        {
            return await _repository.GetSoldEmptiesAsync();
        }

        /// <summary>
        /// Returns all non-refillable empty cartridges that have been Disposed, ordered newest first.
        /// Read-only — for the Disposed Cartridges audit page.
        /// </summary>
        public async Task<List<ClosedEmptyCartridgeDto>> GetDisposedEmptiesAsync()
        {
            return await _repository.GetDisposedEmptiesAsync();
        }

        /// <summary>
        /// Returns all EmptyCartridge rows with ConditionStatus = 'DAMAGED', ordered
        /// by most recent return first.
        /// Read-only — for the Damaged Empty Cartridges page.
        /// </summary>
        public async Task<List<DamagedEmptyCartridgeDto>> GetDamagedEmptiesAsync()
        {
            return await _repository.GetDamagedEmptiesAsync();
        }

        /// <summary>
        /// Returns non-refillable empty cartridges grouped by ReqId + CartridgeModel.
        /// Presentation-only — underlying EmptyCartridge rows are not modified.
        /// </summary>
        public async Task<List<NonRefillableGroupedDto>> GetNonRefillableGroupedAsync()
        {
            return await _repository.GetNonRefillableGroupedAsync();
        }

        // BulkMarkAsDisposedAsync and BulkMarkAsSoldAsync removed — use outbound batch workflow instead.

        /// <summary>
        /// Gets empty cartridges grouped by vendor
        /// Shows cartridges accumulating in boxes (physical only, not tracked in system)
        /// </summary>
        public async Task<List<EmptyCartridgesByVendorDto>> GetEmptyCartridgesByVendorAsync()
        {
            return await _repository.GetEmptyCartridgesByVendorAsync();
        }

        /// <summary>
        /// Gets audit trail for a specific vendor batch
        /// Shows request-level rows that contributed to the batch
        /// READ-ONLY query for audit trail display
        /// </summary>
        public async Task<List<VendorBatchAuditTrailDto>> GetBatchAuditTrailAsync(int batchId)
        {
            return await _repository.GetBatchAuditTrailAsync(batchId);
        }

        /// <summary>
        /// No-op: threshold logic has been removed.
        /// </summary>
        public async Task UpdateBatchThresholdAsync(int batchId, decimal requiredReturnPercent)
        {
            await _repository.UpdateBatchThresholdAsync(batchId, requiredReturnPercent);
        }

        public async Task UpdateBatchVendorAsync(int batchId, int newVendorId)
        {
            if (newVendorId <= 0)
            {
                throw new ArgumentException("Vendor is required.");
            }

            await _repository.UpdateBatchVendorAsync(batchId, newVendorId);
        }

        /// <summary>
        /// Returns all Active refill batches, ordered by vendor name then model.
        /// Used by the manual batch-assignment wizard "Use Existing Active Batch" mode.
        /// </summary>
        public async Task<List<VendorCartridgeBatchDto>> GetAllActiveBatchesAsync()
        {
            return await _repository.GetAllActiveBatchesAsync();
        }

        // =====================================================================
        // OUTBOUND BATCH METHODS (DISPOSE / SELL)
        // =====================================================================

        /// <summary>Returns unassigned empty cartridges eligible for a DISPOSE or SELL batch.</summary>
        public async Task<List<UnassignedReturnDto>> GetUnassignedOutboundCartridgesAsync(bool nonRefillableOnly = false, bool damagedOnly = false)
        {
            return await _repository.GetUnassignedOutboundCartridgesAsync(nonRefillableOnly, damagedOnly);
        }

        /// <summary>Returns all active DISPOSE and SELL batches.</summary>
        public async Task<List<OutboundBatchDto>> GetOutboundBatchesAsync()
        {
            return await _repository.GetOutboundBatchesAsync();
        }

        /// <summary>Returns all finalized DISPOSE and SELL batches (Status = 'Disposed' or 'Sold').</summary>
        public async Task<List<OutboundBatchDto>> GetFinalizedOutboundBatchesAsync()
        {
            return await _repository.GetFinalizedOutboundBatchesAsync();
        }

        /// <summary>Creates a new DISPOSE or SELL batch. Returns the new BatchId.</summary>
        public async Task<int> CreateOutboundBatchAsync<T>(
            int vendorId, IEnumerable<T> modelGroups, string purpose, int createdBy, string remarks = null)
            where T : class
        {
            return await _repository.CreateOutboundBatchAsync(vendorId, modelGroups, purpose, createdBy, remarks);
        }

        /// <summary>Finalizes a DISPOSE/SELL batch: updates batch status and all linked empty cartridges.</summary>
        public async Task FinalizeOutboundBatchAsync(int batchId, int userId)
        {
            await _repository.FinalizeOutboundBatchAsync(batchId, userId);
        }

        /// <summary>Deletes an Active outbound batch and restores its cartridges to Pending.</summary>
        public async Task DeleteOutboundBatchAsync(int batchId, int userId)
        {
            await _repository.DeleteOutboundBatchAsync(batchId, userId);
        }

        /// <summary>Deletes a refill batch and restores its linked cartridges to Pending.</summary>
        public async Task DeleteRefillBatchAsync(int batchId, int userId)
        {
            await _repository.DeleteRefillBatchAsync(batchId, userId);
        }

        /// <summary>
        /// Creates a vendor batch and assigns empty cartridges to it.
        /// Quota-driven dispatch: Only creates batch when initiated by IT and quota is met.
        ///
        /// REFILL-VENDOR DESIGN:
        /// - VendorId = the REFILL vendor, NOT necessarily the original supplier.
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
        public async Task<int> CreateBatchAndSendToVendorAsync(int vendorId, int cartridgeModelId, int requiredQty, int createdBy, string remarks)
        {
            return await _repository.CreateBatchAndAssignEmptiesAsync(vendorId, cartridgeModelId, requiredQty, createdBy, remarks);
        }

        /// <summary>
        /// Marks a batch as ready to be sent to vendor for refill
        /// Status: ThresholdMet → ForReturn
        /// NO inventory changes - this is purely administrative
        /// </summary>
        public async Task MarkBatchForReturnAsync(int batchId, int userId)
        {
            await _repository.MarkBatchForReturnAsync(batchId, userId);
        }


        /// <summary>
        /// Removes cartridge(s) from an Active refill batch and returns them to
        /// the unassigned pool so they can be re-assigned via Assign Returns.
        /// </summary>
        public async Task<int> RemoveFromBatchAsync(
            int batchId, int? reqId, int cartridgeModelId, int fallbackEmptyCartridgeId, int userId)
        {
            return await _repository.RemoveFromBatchAsync(
                batchId, reqId, cartridgeModelId, fallbackEmptyCartridgeId, userId);
        }

        /// <summary>
        /// Transfers cartridge(s) from one Active refill batch to another Active refill batch.
        /// </summary>
        public async Task<int> TransferToBatchAsync(
            int fromBatchId, int toBatchId, int? reqId, int cartridgeModelId, int fallbackEmptyCartridgeId, int userId)
        {
            return await _repository.TransferToBatchAsync(
                fromBatchId, toBatchId, reqId, cartridgeModelId, fallbackEmptyCartridgeId, userId);
        }
    }
}
