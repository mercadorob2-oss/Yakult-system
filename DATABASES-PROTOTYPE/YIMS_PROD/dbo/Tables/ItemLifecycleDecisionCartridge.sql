-- ============================================================
-- Table:   dbo.ItemLifecycleDecisionCartridge
-- Created: 2026-02-23
-- ============================================================
--
-- PURPOSE
--   Cartridge-domain extension to dbo.ItemLifecycleDecision.
--   Provides the FK back to dbo.EmptyCartridge for lifecycle
--   decisions that originate from the cartridge return workflow.
--
-- WHY A SEPARATE TABLE (not a column in ItemLifecycleDecision)
--   dbo.ItemLifecycleDecision is item-type agnostic by design.
--   Embedding EmptyCartridgeId there would:
--     • Pollute the base table with a NULL column for every
--       non-cartridge item decision (hardware, software, etc.).
--     • Imply a cartridge dependency for item types that have
--       no empty-return workflow.
--     • Require a schema change if hardware ever develops its
--       own extension needs — which it would not, under this model.
--
-- RELATIONSHIP
--   One-to-one with dbo.ItemLifecycleDecision (UNIQUE on DecisionId).
--   A row in this table exists only for cartridge items whose
--   decision originated from a specific EmptyCartridge return.
--   Absence of a row means either the item is not a cartridge,
--   or no EmptyCartridge row was involved.
--
-- AUTHORITATIVE CARTRIDGE STATUS
--   dbo.EmptyCartridge.Status ('Disposed' | 'Sold') remains the
--   authoritative status field for the cartridge workflow.
--   This table complements it by linking the formal lifecycle
--   decision record to the originating return record.
--
-- APPLICATION RESPONSIBILITY
--   When executing a cartridge lifecycle decision:
--     1. Write the parent row to dbo.ItemLifecycleDecision first.
--     2. Write this extension row in the same transaction.
--     3. Set EmptyCartridge.Status = 'Disposed' | 'Sold' on the
--        linked EmptyCartridgeId in the same transaction.
--   All three writes must be atomic.
-- ============================================================
CREATE TABLE [dbo].[ItemLifecycleDecisionCartridge] (
    [DecisionCartridgeId] INT NOT NULL IDENTITY (1, 1),

    -- FK to the parent lifecycle decision row.
    -- UNIQUE enforces the one-to-one relationship.
    [DecisionId]          INT NOT NULL,

    -- The EmptyCartridge return record that triggered this decision.
    -- NOT NULL: this extension table only exists when there is a
    -- concrete return record to link to.
    [EmptyCartridgeId]    INT NOT NULL,

    -- ── Constraints ─────────────────────────────────────────
    CONSTRAINT [PK_ItemLifecycleDecisionCartridge]
        PRIMARY KEY CLUSTERED ([DecisionCartridgeId] ASC),

    -- One extension row per decision.
    CONSTRAINT [UQ_ItemLifecycleDecisionCartridge_Decision]
        UNIQUE NONCLUSTERED ([DecisionId] ASC),

    -- ── Foreign keys ────────────────────────────────────────
    CONSTRAINT [FK_ItemLifecycleDecisionCartridge_Decision]
        FOREIGN KEY ([DecisionId])
        REFERENCES [dbo].[ItemLifecycleDecision] ([DecisionId]),

    CONSTRAINT [FK_ItemLifecycleDecisionCartridge_EmptyCartridge]
        FOREIGN KEY ([EmptyCartridgeId])
        REFERENCES [dbo].[EmptyCartridge] ([EmptyCartridgeId])
);
GO

-- ── Indexes ─────────────────────────────────────────────────

-- Reverse-lookup: given an EmptyCartridgeId, find its decision.
-- Supports reporting queries like "was this return ever actioned?"
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecisionCartridge_EmptyCartridgeId]
    ON [dbo].[ItemLifecycleDecisionCartridge] ([EmptyCartridgeId] ASC)
    INCLUDE ([DecisionId]);
GO
