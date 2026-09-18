-- ============================================================
-- Table:   dbo.ItemDecisionType
-- Purpose: Lookup table for the category of lifecycle decision
--          that can be applied to any item record.
--
-- Values (seed rows below):
--   SELL    — item is transferred to a party in exchange for value
--   DISPOSE — item is physically disposed of (scrapped, recycled,
--             destroyed); no monetary return expected
--
-- Item-type scope:
--   Applies to cartridge and non-cartridge items equally.
--   This table carries no cartridge-specific columns or logic.
--
-- Extensibility:
--   New decision types (e.g. DONATE, TRANSFER) require only an
--   INSERT here; no schema change to dependent tables.
-- ============================================================
CREATE TABLE [dbo].[ItemDecisionType] (
    [DecisionTypeId]   INT            IDENTITY (1, 1) NOT NULL,
    [DecisionTypeName] NVARCHAR (20)  NOT NULL,
    [Description]      NVARCHAR (200) NULL,

    CONSTRAINT [PK_ItemDecisionType]      PRIMARY KEY CLUSTERED  ([DecisionTypeId] ASC),
    CONSTRAINT [UQ_ItemDecisionType_Name] UNIQUE NONCLUSTERED    ([DecisionTypeName] ASC)
);
GO

-- ── Seed rows ───────────────────────────────────────────────
INSERT INTO [dbo].[ItemDecisionType] ([DecisionTypeName], [Description])
VALUES
    ('SELL',    'Item is sold to an external or internal party'),
    ('DISPOSE', 'Item is physically disposed of (scrapped, recycled, or destroyed)');
GO
