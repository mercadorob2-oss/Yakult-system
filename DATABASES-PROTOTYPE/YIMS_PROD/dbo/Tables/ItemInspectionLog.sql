-- ============================================================
-- Table:   dbo.ItemInspectionLog
-- Created: 2026-02-23
-- ============================================================
--
-- PURPOSE
--   Records one or more condition inspections performed on an
--   item before a final lifecycle decision is committed.
--
-- ITEM-TYPE SCOPE
--   Item-type agnostic.  Applies to cartridges and non-cartridge
--   items (hardware, software/license, services) equally.
--   Contains no cartridge-specific columns.
--
-- RELATIONSHIP TO dbo.ItemLifecycleDecision
--   Inspection rows are independent of lifecycle decision rows.
--   Both reference dbo.Item.ItemId, but there is no enforced FK
--   between them because:
--     • An item may have inspections but no decision yet.
--     • A decision may be made with no prior inspections.
--   The application links them via ItemId when building the
--   item's decision history view.
--
-- RELATIONSHIP TO dbo.Item.ConditionID
--   dbo.Item.ConditionID is the current working condition of the
--   item and may be updated at any time.
--   ItemInspectionLog.ConditionId is a POINT-IN-TIME snapshot of
--   what the inspector observed on that date.  The two values are
--   independent and may diverge.
--
-- RECOMMENDATION
--   Non-binding assessment from a single inspection pass.
--   Does not determine the final decision; that lives in
--   dbo.ItemLifecycleDecision.
--   NULL is allowed when the inspector records observations only.
--
-- MULTIPLE INSPECTORS
--   Multiple rows per item are permitted.  Different people may
--   inspect the same item on different dates and reach different
--   recommendations.  The final lifecycle decision consolidates
--   these inputs.
-- ============================================================
CREATE TABLE [dbo].[ItemInspectionLog] (
    [InspectionId]   INT            IDENTITY (1, 1) NOT NULL,

    -- ── Subject ─────────────────────────────────────────────
    [ItemId]         INT            NOT NULL,
    -- Condition observed by the inspector at this point in time.
    [ConditionId]    INT            NULL,          -- FK → dbo.Condition

    -- ── Audit ───────────────────────────────────────────────
    [InspectedAt]    DATETIME2 (2)  CONSTRAINT [DF_ItemInspectionLog_InspectedAt]
                                    DEFAULT (sysutcdatetime()) NOT NULL,
    [InspectedBy]    INT            NOT NULL,      -- FK → dbo.[User].UserId

    -- ── Outcome ─────────────────────────────────────────────
    -- Non-binding recommendation from this inspection pass.
    -- 'SELL'    — inspector recommends selling
    -- 'DISPOSE' — inspector recommends disposal
    -- 'REPAIR'  — inspector recommends repair before any decision
    -- 'RETAIN'  — inspector recommends keeping in active inventory
    -- NULL      — no recommendation (observation only)
    [Recommendation] NVARCHAR (20)  NULL,
    [Notes]          NVARCHAR (500) NULL,

    -- ── Constraints ─────────────────────────────────────────
    CONSTRAINT [PK_ItemInspectionLog]
        PRIMARY KEY CLUSTERED ([InspectionId] ASC),

    CONSTRAINT [CK_ItemInspectionLog_Recommendation]
        CHECK ([Recommendation] IN ('SELL', 'DISPOSE', 'REPAIR', 'RETAIN')
               OR [Recommendation] IS NULL),

    -- ── Foreign keys ────────────────────────────────────────
    CONSTRAINT [FK_ItemInspectionLog_Item]
        FOREIGN KEY ([ItemId])
        REFERENCES [dbo].[Item] ([ItemId]),

    CONSTRAINT [FK_ItemInspectionLog_Condition]
        FOREIGN KEY ([ConditionId])
        REFERENCES [dbo].[Condition] ([ConditionID]),

    CONSTRAINT [FK_ItemInspectionLog_InspectedBy]
        FOREIGN KEY ([InspectedBy])
        REFERENCES [dbo].[User] ([UserId])
);
GO

-- ── Indexes ─────────────────────────────────────────────────

-- Primary access pattern: full inspection history for a given item.
CREATE NONCLUSTERED INDEX [IX_ItemInspectionLog_ItemId]
    ON [dbo].[ItemInspectionLog] ([ItemId] ASC, [InspectedAt] DESC)
    INCLUDE ([ConditionId], [Recommendation], [InspectedBy]);
GO

-- Date-range audits: all inspections in a period across all items.
CREATE NONCLUSTERED INDEX [IX_ItemInspectionLog_InspectedAt]
    ON [dbo].[ItemInspectionLog] ([InspectedAt] DESC)
    INCLUDE ([ItemId], [InspectedBy], [Recommendation]);
GO
