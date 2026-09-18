-- Migration: Create dbo.ConsumableModel
-- Purpose : Formal grouping table for Ink / Toner / Print Head consumables, mirroring
--           dbo.CartridgeModel. Each physical dbo.Item catalog row for one of these
--           categories carries a ConsumableModelId FK, and available stock is computed
--           by summing StockOnHand across every Item row tied to the same model — not
--           by matching free-text Name/Category strings, which silently pooled stock
--           across accidental duplicate Item rows (e.g. EPSON 001 (BLUE), HP CF276X).
-- Note    : No Brand New / Refilled condition split here (unlike CartridgeModel) — a
--           single stock number per model is enough for these categories.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ConsumableModel' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[ConsumableModel] (
        [ConsumableModelId] INT            IDENTITY (1, 1) NOT NULL,
        [ModelNumber]       NVARCHAR (200) NOT NULL,
        [Category]          NVARCHAR (100) NOT NULL,
        [IsRequestable]     BIT            DEFAULT ((1)) NOT NULL,
        [IsActive]          BIT            DEFAULT ((1)) NOT NULL,
        [CreatedAt]         DATETIME2 (7)  DEFAULT (sysdatetime()) NOT NULL,
        [CreatedBy]         INT            NOT NULL,
        CONSTRAINT [PK_ConsumableModel] PRIMARY KEY CLUSTERED ([ConsumableModelId] ASC),
        CONSTRAINT [UQ_ConsumableModel_ModelNumber_Category] UNIQUE ([ModelNumber] ASC, [Category] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_ConsumableModel_Requestable]
        ON [dbo].[ConsumableModel]([IsActive] ASC, [IsRequestable] ASC)
        INCLUDE([ConsumableModelId], [ModelNumber], [Category]);

    PRINT 'Table dbo.ConsumableModel created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.ConsumableModel already exists. Skipping.';
END
GO
