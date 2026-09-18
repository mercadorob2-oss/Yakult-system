-- Migration: Add IssuedBrandNewQty and IssuedRefilledQty columns to dbo.Set
-- Purpose: Track issued cartridge quantities by condition (Brand New vs Refilled)
-- Date: 2026-02-19

-- Add columns if they don't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Set') AND name = 'IssuedBrandNewQty')
BEGIN
    ALTER TABLE dbo.[Set]
    ADD IssuedBrandNewQty INT NULL;
    
    PRINT 'Added column IssuedBrandNewQty to dbo.Set';
END
ELSE
BEGIN
    PRINT 'Column IssuedBrandNewQty already exists in dbo.Set';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Set') AND name = 'IssuedRefilledQty')
BEGIN
    ALTER TABLE dbo.[Set]
    ADD IssuedRefilledQty INT NULL;
    
    PRINT 'Added column IssuedRefilledQty to dbo.Set';
END
ELSE
BEGIN
    PRINT 'Column IssuedRefilledQty already exists in dbo.Set';
END

-- Create index for performance
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Set') AND name = 'IX_Set_IssuedQuantities')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Set_IssuedQuantities
    ON dbo.[Set] (SetId)
    INCLUDE (IssuedBrandNewQty, IssuedRefilledQty)
    WHERE IssuedBrandNewQty IS NOT NULL OR IssuedRefilledQty IS NOT NULL;
    
    PRINT 'Created index IX_Set_IssuedQuantities on dbo.Set';
END
ELSE
BEGIN
    PRINT 'Index IX_Set_IssuedQuantities already exists on dbo.Set';
END

GO
