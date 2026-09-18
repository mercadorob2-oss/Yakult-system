-- ============================================================
-- Table:   dbo.ItemLifecycleDecision
-- Created: 2026-02-23
-- ============================================================
--
-- PURPOSE
--   Stores lifecycle decisions (Sell / Dispose) for any item in
--   dbo.Item, regardless of item type.
--
-- ITEM-TYPE SCOPE
--   ┌─────────────────────────────┬───────────────────────────┐
--   │ Cartridge items             │ Non-cartridge items       │
--   ├─────────────────────────────┼───────────────────────────┤
--   │ Proven, production path.    │ Future onboarding.        │
--   │ EmptyCartridge.Status is    │ No existing sell/dispose  │
--   │ the authoritative status.   │ workflow yet.             │
--   │ This table is an additive   │ This table will be the    │
--   │ lifecycle record — it does  │ primary lifecycle record  │
--   │ NOT replace EmptyCartridge. │ when those workflows land.│
--   └─────────────────────────────┴───────────────────────────┘
--   This table contains NO cartridge-specific columns.
--   Cartridge linkage lives in dbo.ItemLifecycleDecisionCartridge.
--
-- DECISION STATUS STATE MACHINE
--   Pending  ──► Executed   (decision applied; inventory adjusted)
--   Pending  ──► Cancelled  (decision overruled before execution)
--
--   Only one row per item may reach 'Executed' (enforced via a
--   filtered unique index below).  Multiple 'Pending' rows are
--   allowed to support multi-stakeholder proposal workflows.
--   'Cancelled' rows are retained for audit.
--
-- APPLICATION RESPONSIBILITY (on transition to 'Executed')
--   1. POST a Negative entry to dbo.Inventory (EntryType='Negative').
--   2. DECREMENT dbo.Item.StockOnHand by Quantity.
--   3. SET dbo.Item.Active = 0.
--   4. INSERT into dbo.ArchiveStatus (EntityType = 'Item').
--   5. For cartridge items ONLY:
--      a. UPDATE dbo.EmptyCartridge.Status = 'Disposed' | 'Sold'
--         on the linked EmptyCartridgeId (via the extension table).
--      b. INSERT into dbo.CartridgeMovement (MovementType='Adjustment').
--
-- RECIPIENTNAME
--   Optional free text — who or what received the item.
--   Generalises EmptyCartridge.DisposalCompanyName to all item
--   types.  Not an FK to dbo.Vendor because the recipient may be
--   an individual, an unlisted company, or an internal department.
--
-- SALEAMOUNT
--   Applicable only when DecisionTypeId resolves to 'SELL'.
--   Should be NULL for DISPOSE decisions.
-- ============================================================
CREATE TABLE [dbo].[ItemLifecycleDecision] (
    [DecisionId]     INT             IDENTITY (1, 1)  NOT NULL,

    -- ── Subject ─────────────────────────────────────────────
    -- References dbo.Item directly.  No cartridge-specific FK.
    [ItemId]         INT             NOT NULL,
    [DecisionTypeId] INT             NOT NULL,   -- FK → dbo.ItemDecisionType
    -- Condition of the item at the moment this decision was recorded.
    -- Stored as a snapshot; may differ from dbo.Item.ConditionID if the
    -- item's condition was updated independently after this row was written.
    [ConditionId]    INT             NULL,        -- FK → dbo.Condition
    [Quantity]       INT             CONSTRAINT [DF_ItemLifecycleDecision_Quantity]
                                     DEFAULT ((1)) NOT NULL,

    -- ── State machine ───────────────────────────────────────
    -- 'Pending'  — proposed; awaiting approval or execution
    -- 'Executed' — inventory impact applied; item archived (terminal)
    -- 'Cancelled'— overruled before execution; retained for audit
    [DecisionStatus] NVARCHAR (20)   CONSTRAINT [DF_ItemLifecycleDecision_Status]
                                     DEFAULT ('Pending') NOT NULL,

    -- ── Audit ───────────────────────────────────────────────
    [DecidedAt]      DATETIME2 (2)   CONSTRAINT [DF_ItemLifecycleDecision_DecidedAt]
                                     DEFAULT (sysutcdatetime()) NOT NULL,
    [DecidedBy]      INT             NOT NULL,   -- FK → dbo.[User].UserId

    -- ── Outcome metadata ────────────────────────────────────
    -- Company or person that received the item.
    [RecipientName]  NVARCHAR (200)  NULL,
    -- Monetary amount for SELL decisions; NULL for DISPOSE.
    [SaleAmount]     DECIMAL (18, 2) NULL,
    [Remarks]        NVARCHAR (500)  NULL,

    -- ── Constraints ─────────────────────────────────────────
    CONSTRAINT [PK_ItemLifecycleDecision]
        PRIMARY KEY CLUSTERED ([DecisionId] ASC),

    CONSTRAINT [CK_ItemLifecycleDecision_Status]
        CHECK ([DecisionStatus] IN ('Pending', 'Executed', 'Cancelled')),

    CONSTRAINT [CK_ItemLifecycleDecision_Quantity]
        CHECK ([Quantity] > 0),

    -- ── Foreign keys ────────────────────────────────────────
    CONSTRAINT [FK_ItemLifecycleDecision_Item]
        FOREIGN KEY ([ItemId])
        REFERENCES [dbo].[Item] ([ItemId]),

    CONSTRAINT [FK_ItemLifecycleDecision_DecisionType]
        FOREIGN KEY ([DecisionTypeId])
        REFERENCES [dbo].[ItemDecisionType] ([DecisionTypeId]),

    CONSTRAINT [FK_ItemLifecycleDecision_Condition]
        FOREIGN KEY ([ConditionId])
        REFERENCES [dbo].[Condition] ([ConditionID]),

    CONSTRAINT [FK_ItemLifecycleDecision_DecidedBy]
        FOREIGN KEY ([DecidedBy])
        REFERENCES [dbo].[User] ([UserId])
);
GO

-- ── Indexes ─────────────────────────────────────────────────

-- Core business rule: exactly one Executed decision per item.
-- Filtered so Pending and Cancelled rows are excluded — they do not
-- compete for this uniqueness slot.
CREATE UNIQUE NONCLUSTERED INDEX [UQ_ItemLifecycleDecision_Executed]
    ON [dbo].[ItemLifecycleDecision] ([ItemId] ASC)
    WHERE [DecisionStatus] = 'Executed';
GO

-- Fast lookup of all decisions for a given item (history view, audit).
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_ItemId]
    ON [dbo].[ItemLifecycleDecision] ([ItemId] ASC, [DecidedAt] DESC)
    INCLUDE ([DecisionTypeId], [DecisionStatus], [DecidedBy]);
GO

-- Date-range reporting: "all SELL decisions in February 2026".
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_DecidedAt]
    ON [dbo].[ItemLifecycleDecision] ([DecidedAt] DESC)
    INCLUDE ([ItemId], [DecisionTypeId], [DecisionStatus], [DecidedBy]);
GO

-- Filter by decision type across all items and dates.
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_DecisionType]
    ON [dbo].[ItemLifecycleDecision] ([DecisionTypeId] ASC, [DecisionStatus] ASC)
    INCLUDE ([ItemId], [DecidedAt]);
GO
