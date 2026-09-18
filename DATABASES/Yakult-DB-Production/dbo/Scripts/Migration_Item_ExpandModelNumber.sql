-- ============================================================
-- Migration: Expand dbo.Item.[ModelNumber] from NVARCHAR(100)
--            to NVARCHAR(500) to support long model strings.
-- ============================================================

ALTER TABLE [dbo].[Item]
    ALTER COLUMN [ModelNumber] NVARCHAR(500) NULL;
GO

PRINT 'dbo.Item.ModelNumber expanded to NVARCHAR(500).';
