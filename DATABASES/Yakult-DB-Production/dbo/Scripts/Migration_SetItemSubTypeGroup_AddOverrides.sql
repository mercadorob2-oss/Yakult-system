-- ViewInvoiceDetailPage lets the user type a Subtotal/VAT%/WHT%/Discount% per Sub-Type
-- Group card as a working calculator. Without a place to persist those values, every page
-- reload re-derived the group's Subtotal from SUM(dbo.SetItem.Amount) — the real invoiced
-- line amounts — which silently discarded whatever the user had typed and already saved,
-- resetting the group (and therefore the invoice header, which sums group Totals) back to
-- whatever the raw item amounts computed to. These columns let the user's last-entered
-- values round-trip correctly. NULL means "not yet overridden" — callers fall back to the
-- computed SUM(Amount)/header default percentages in that case.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'SubtotalOverride'
)
BEGIN
    ALTER TABLE dbo.SetItemSubTypeGroup ADD SubtotalOverride DECIMAL (18, 2) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'VatPercent'
)
BEGIN
    ALTER TABLE dbo.SetItemSubTypeGroup ADD VatPercent DECIMAL (9, 4) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'WhtPercent'
)
BEGIN
    ALTER TABLE dbo.SetItemSubTypeGroup ADD WhtPercent DECIMAL (9, 4) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'DiscountPercent'
)
BEGIN
    ALTER TABLE dbo.SetItemSubTypeGroup ADD DiscountPercent DECIMAL (9, 4) NULL;
END
GO
