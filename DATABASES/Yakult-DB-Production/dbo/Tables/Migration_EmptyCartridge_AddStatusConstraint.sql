-- ============================================================
-- Migration: CK_EmptyCartridge_Status
-- Date:      2026-02-21
-- ============================================================
--
-- PURPOSE
--   dbo.EmptyCartridge.Status has no CHECK constraint.  All
--   status transitions are currently enforced only by application
--   logic, leaving the column open to arbitrary values.
--
--   This migration adds CK_EmptyCartridge_Status to enumerate
--   every valid status, including two new terminal values that
--   close out non-refillable empty cartridges retained by IT:
--
--     'Disposed' -- physically disposed of by IT; negative
--                   inventory entry recorded in dbo.Inventory.
--     'Sold'     -- sold (internal resale) by IT; negative
--                   inventory entry recorded in dbo.Inventory.
--
-- HOW DISPOSE / SELL WORKS (application flow, database view)
--   When IT marks a non-refillable empty as Disposed or Sold:
--
--     1. UPDATE dbo.EmptyCartridge
--           SET Status      = 'Disposed' | 'Sold',
--               DateModified = GETDATE(),
--               ModifiedBy   = @UserId
--         WHERE EmptyCartridgeId = @EmptyCartridgeId;
--
--     2. UPDATE dbo.Item                            -- delta-based
--           SET StockOnHand  = StockOnHand - @Qty,  -- negative delta
--               DateModified = GETDATE(),
--               ModifiedBy   = @UserId
--         WHERE ItemId = @ITCustodyItemId;           -- the Item row
--                                                   -- created at return
--
--     3. INSERT INTO dbo.Inventory                  -- existing mechanism
--            (EntryType, Quantity, ItemId, PostedBy, Description, ConditionID)
--         VALUES
--            ('Negative', @Qty, @ITCustodyItemId,   -- EntryType='Negative'
--              @UserId,                              -- already has a CHECK
--             'Non-refillable cartridge [Disposed|Sold] - EmptyCartridgeId ' + CAST(@EmptyCartridgeId AS NVARCHAR),
--              @ConditionID);
--
--     4. INSERT INTO dbo.CartridgeMovement           -- existing mechanism
--            (ItemId, Quantity, MovementType, CartridgeTypeId,
--             EmployeeId, BranchId, DeptId, ReferenceRequestId,
--             CreatedBy, Remarks)
--         VALUES
--            (@ITCustodyItemId, @Qty, 'Adjustment',  -- MovementType='Adjustment'
--              @CartridgeTypeId,                     -- already has a CHECK
--              @UserId, @BranchId, @DeptId, @ReqId,
--              @UserId,
--             '[Disposed|Sold] non-refillable empty - EmptyCartridgeId ' + CAST(@EmptyCartridgeId AS NVARCHAR));
--
-- REFILL GUARD
--   vw_NonRefillableEmptyCartridges (updated in its own view file)
--   adds AND ec.Status NOT IN ('Disposed', 'Sold') so that
--   closed empties are invisible to IT's pending-action list.
--
--   vw_RefillBatchCandidates and vw_UnassignedRefillableEmpties
--   already filter WHERE ISNULL(cm.IsRefillable, 1) = 1 and are
--   unaffected — non-refillable empties never reach those views.
--
-- EXISTING STATUS VALUES (observed in application code)
--   'Pending'        default at return; awaiting action
--   'BatchAssigned'  assigned to a vendor refill batch (refillable only)
--   'SentForRefill'  sent to vendor (refillable only)
--   'SentToVendor'   in transit to vendor (refillable only)
--   'InRefill'       being refilled by vendor (refillable only)
--   'ForReturn'      being returned from vendor (refillable only)
--   'Completed'      refill confirmed, stock restored (refillable only)
--
-- PRE-FLIGHT CHECK: list any Status values not in the new enum
--   (must return 0 rows before adding the constraint)
--
--   SELECT DISTINCT Status
--   FROM   dbo.EmptyCartridge
--   WHERE  Status NOT IN (
--              'Pending','BatchAssigned','SentForRefill','SentToVendor',
--              'InRefill','ForReturn','Completed','Disposed','Sold');
-- ============================================================

-- ── 1. Add the Status CHECK constraint ───────────────────────
ALTER TABLE [dbo].[EmptyCartridge]
    ADD CONSTRAINT [CK_EmptyCartridge_Status]
    CHECK ([Status] IN (
        'Pending',         -- returned, awaiting action
        'BatchAssigned',   -- assigned to vendor refill batch  (refillable only)
        'SentForRefill',   -- sent to vendor for refill        (refillable only)
        'SentToVendor',    -- in transit to vendor             (refillable only)
        'InRefill',        -- being refilled by vendor         (refillable only)
        'ForReturn',       -- being returned from vendor       (refillable only)
        'Completed',       -- refill confirmed, stock restored (refillable only)
        'Disposed',        -- disposed of by IT                (non-refillable only)
        'Sold'             -- sold / internal resale by IT     (non-refillable only)
    ));
GO

-- ── 2. Filtered index: fast lookup of IT-pending empties ─────
--   Supports the most common IT query:
--   "Which non-refillable empties still need to be actioned?"
--   Excludes Disposed and Sold rows (terminal states) from the
--   index so the index stays small as history accumulates.
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_NonRefillable_Pending]
    ON [dbo].[EmptyCartridge] ([CartridgeModelId] ASC, [Status] ASC)
    INCLUDE ([Quantity], [ReturnedAt], [ReqId], [ConditionId])
    WHERE [Status] IN ('Pending', 'Disposed', 'Sold')    -- covers pending + terminal for filtering
      AND [VendorBatchId] IS NULL;                        -- non-refillable empties never have a batch
GO
