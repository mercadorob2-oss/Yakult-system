-- Migration: Add Title row to CARTRIDGE_EXCHANGE_* email templates
--
-- Inserts a {RequesterTitle} row immediately after the {RequesterName} (Employee)
-- row in the Set Details section of all three cartridge exchange templates:
--   CARTRIDGE_EXCHANGE_FULFILLED
--   CARTRIDGE_EXCHANGE_PARTIAL
--   CARTRIDGE_EXCHANGE_UNFULFILLED
--
-- Safe to run multiple times: each template is skipped if RequesterTitle
-- is already present.
--
-- NOTE: These templates are also rebuilt by EnsureCartridgeEmailTemplatesAsync
-- on the next app run, which will include RequesterTitle from the updated
-- BuildSetCartridgeEmailBody method. This script makes the DB immediately
-- consistent without waiting for that.

DECLARE @TitleRow NVARCHAR(512) =
    '<tr><td style="padding:8px 0; color:#666;"><strong>Title:</strong></td>'
    + '<td style="padding:8px 0;">{RequesterTitle}</td></tr>';

-- ── CARTRIDGE_EXCHANGE_FULFILLED ─────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_FULFILLED'
      AND BodyTemplate LIKE '%RequesterTitle%'
)
BEGIN
    UPDATE dbo.EmailTemplate
    SET BodyTemplate = REPLACE(
        BodyTemplate,
        '{RequesterName}</td></tr>',
        '{RequesterName}</td></tr>' + CHAR(13)+CHAR(10) + @TitleRow
    )
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_FULFILLED'
      AND BodyTemplate LIKE '%{RequesterName}</td></tr>%';

    IF @@ROWCOUNT > 0
        PRINT 'CARTRIDGE_EXCHANGE_FULFILLED updated — RequesterTitle row added.';
    ELSE
        PRINT 'WARNING: CARTRIDGE_EXCHANGE_FULFILLED — {RequesterName}</td></tr> pattern not found. Skipped.';
END
ELSE
    PRINT 'CARTRIDGE_EXCHANGE_FULFILLED already contains RequesterTitle — skipped.';

-- ── CARTRIDGE_EXCHANGE_PARTIAL ───────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_PARTIAL'
      AND BodyTemplate LIKE '%RequesterTitle%'
)
BEGIN
    UPDATE dbo.EmailTemplate
    SET BodyTemplate = REPLACE(
        BodyTemplate,
        '{RequesterName}</td></tr>',
        '{RequesterName}</td></tr>' + CHAR(13)+CHAR(10) + @TitleRow
    )
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_PARTIAL'
      AND BodyTemplate LIKE '%{RequesterName}</td></tr>%';

    IF @@ROWCOUNT > 0
        PRINT 'CARTRIDGE_EXCHANGE_PARTIAL updated — RequesterTitle row added.';
    ELSE
        PRINT 'WARNING: CARTRIDGE_EXCHANGE_PARTIAL — {RequesterName}</td></tr> pattern not found. Skipped.';
END
ELSE
    PRINT 'CARTRIDGE_EXCHANGE_PARTIAL already contains RequesterTitle — skipped.';

-- ── CARTRIDGE_EXCHANGE_UNFULFILLED ───────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_UNFULFILLED'
      AND BodyTemplate LIKE '%RequesterTitle%'
)
BEGIN
    UPDATE dbo.EmailTemplate
    SET BodyTemplate = REPLACE(
        BodyTemplate,
        '{RequesterName}</td></tr>',
        '{RequesterName}</td></tr>' + CHAR(13)+CHAR(10) + @TitleRow
    )
    WHERE TemplateKey = 'CARTRIDGE_EXCHANGE_UNFULFILLED'
      AND BodyTemplate LIKE '%{RequesterName}</td></tr>%';

    IF @@ROWCOUNT > 0
        PRINT 'CARTRIDGE_EXCHANGE_UNFULFILLED updated — RequesterTitle row added.';
    ELSE
        PRINT 'WARNING: CARTRIDGE_EXCHANGE_UNFULFILLED — {RequesterName}</td></tr> pattern not found. Skipped.';
END
ELSE
    PRINT 'CARTRIDGE_EXCHANGE_UNFULFILLED already contains RequesterTitle — skipped.';
