-- ============================================================
-- Migration: ArchiveStatus — add 'EmptyCartridge' entity type
-- Date:      2026-02-21
-- Reason:    Non-refillable empty cartridges that are Sold or
--            Disposed must be archived for audit and reporting.
--            The existing CHECK constraint did not include
--            'EmptyCartridge' as a valid EntityType.
-- ============================================================

-- Drop the existing constraint, then recreate with the new value.
-- All previously valid entity types are preserved exactly.

ALTER TABLE [dbo].[ArchiveStatus]
    DROP CONSTRAINT [CK_ArchiveStatus_EntityType];
GO

ALTER TABLE [dbo].[ArchiveStatus]
    ADD CONSTRAINT [CK_ArchiveStatus_EntityType]
    CHECK ([EntityType] IN (
        'Renewal',
        'Condition',
        'ItemCategory',
        'Vendor',
        'Employee',
        'Branch',
        'Department',
        'Company',
        'Set',
        'Request',
        'Inventory',
        'Item',
        'CartridgeModel',   -- added by CartridgeModel archiving feature
        'EmptyCartridge'    -- added by non-refillable dispose/sell feature
    ));
GO
