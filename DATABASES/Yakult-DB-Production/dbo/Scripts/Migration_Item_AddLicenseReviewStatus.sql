-- ============================================================
-- Migration: dbo.Item — add manual license-review classification
-- Date:      2026-07-26
--
-- BACKGROUND
--   Feeds the new "Invoice License Review" page. That page shows every
--   Hardware-type invoice line from dbo.vw_InvoiceNonLicensedItems (a
--   read-only reporting view — see CreateView_vw_InvoiceNonLicensedItems.sql)
--   and lets a staff member manually classify each one, after physically
--   checking the original sales invoice, as either:
--     - a genuine licensable hardware exception (e.g. a Database Server
--       license, Cisco IP Phone service) that legitimately belongs on the
--       invoice, or
--     - hardware that was mistakenly attached to an invoice.
--   There is no way to derive this automatically from existing columns —
--   see the notes in CreateView_vw_InvoiceNonLicensedItems.sql for why.
--
--   The view itself is READ ONLY and must never be written to directly.
--   This migration adds the actual writable columns, on dbo.Item (the
--   classification is a property of the item, not of one specific invoice
--   line — the same Hardware item appearing on multiple invoices only
--   needs to be reviewed once).
--
-- WHAT THIS ADDS
--   dbo.Item.LicenseReviewStatus NVARCHAR(20) NULL
--     NULL       = not yet reviewed (still shows up in the review queue)
--     'Licensed'    = confirmed genuine licensable hardware exception
--     'NonLicensed' = confirmed mistakenly-invoiced hardware
--   dbo.Item.ReviewedBy  INT NULL       (FK -> dbo.[User].UserId)
--   dbo.Item.ReviewedAt  DATETIME2(7) NULL
--
-- Safe to run repeatedly — every step is guarded.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'LicenseReviewStatus'
)
BEGIN
    ALTER TABLE dbo.Item ADD LicenseReviewStatus NVARCHAR(20) NULL;
    PRINT 'dbo.Item.LicenseReviewStatus added.';
END
ELSE
BEGIN
    PRINT 'dbo.Item.LicenseReviewStatus already exists — skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'ReviewedBy'
)
BEGIN
    ALTER TABLE dbo.Item ADD ReviewedBy INT NULL;
    PRINT 'dbo.Item.ReviewedBy added.';
END
ELSE
BEGIN
    PRINT 'dbo.Item.ReviewedBy already exists — skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'ReviewedAt'
)
BEGIN
    ALTER TABLE dbo.Item ADD ReviewedAt DATETIME2(7) NULL;
    PRINT 'dbo.Item.ReviewedAt added.';
END
ELSE
BEGIN
    PRINT 'dbo.Item.ReviewedAt already exists — skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Item_LicenseReviewStatus'
      AND parent_object_id = OBJECT_ID('dbo.Item')
)
BEGIN
    ALTER TABLE dbo.Item
    ADD CONSTRAINT CK_Item_LicenseReviewStatus
        CHECK (LicenseReviewStatus IS NULL OR LicenseReviewStatus IN ('Licensed', 'NonLicensed'));

    PRINT 'CK_Item_LicenseReviewStatus added.';
END
ELSE
BEGIN
    PRINT 'CK_Item_LicenseReviewStatus already exists — skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Item_ReviewedBy'
)
BEGIN
    ALTER TABLE dbo.Item
    ADD CONSTRAINT FK_Item_ReviewedBy FOREIGN KEY (ReviewedBy) REFERENCES dbo.[User] (UserId);

    PRINT 'FK_Item_ReviewedBy added.';
END
ELSE
BEGIN
    PRINT 'FK_Item_ReviewedBy already exists — skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Item_LicenseReviewStatus' AND object_id = OBJECT_ID('dbo.Item')
)
BEGIN
    -- Filtered index: only unreviewed Hardware rows matter for the review-queue view's
    -- WHERE clause, so index just those instead of the whole (mostly-reviewed-over-time) table.
    CREATE NONCLUSTERED INDEX IX_Item_LicenseReviewStatus
        ON dbo.Item(ItemType)
        INCLUDE (LicenseReviewStatus)
        WHERE LicenseReviewStatus IS NULL;

    PRINT 'IX_Item_LicenseReviewStatus added.';
END
ELSE
BEGIN
    PRINT 'IX_Item_LicenseReviewStatus already exists — skipped.';
END
GO
