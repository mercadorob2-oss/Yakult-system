-- Migration: Add Title to VIEWSET_DEPLOYMENT_SUCCESS email template
--
-- Two changes:
--   1. Greeting line: replaces <strong>{RequesterName}</strong> with
--      <strong>{RequesterDisplayName}</strong> so the greeting reads
--      "Hi Mr. John Doe," instead of "Hi John Doe,".
--      ({RequesterDisplayName} = "{Title} {Name}" when a title exists, else just "{Name}")
--
--   2. Details table: inserts a Title row immediately before the Branch row.
--
-- Uses CHARINDEX + STUFF for the table insertion so variable whitespace/
-- indentation in the stored template is not a problem.
--
-- Safe to run multiple times: skips if RequesterDisplayName is already present.

DECLARE @TemplateKey NVARCHAR(100) = 'VIEWSET_DEPLOYMENT_SUCCESS';
DECLARE @body        NVARCHAR(MAX);
DECLARE @chunk       NVARCHAR(MAX);
DECLARE @revPos      INT;
DECLARE @trPos       INT;
DECLARE @TitleRow    NVARCHAR(MAX);

-- ── Verify template exists ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE TemplateKey = @TemplateKey)
BEGIN
    PRINT 'Template VIEWSET_DEPLOYMENT_SUCCESS not found — skipped.';
    RETURN;
END

-- ── Skip if already migrated ─────────────────────────────────────────────────
IF EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey  = @TemplateKey
      AND BodyTemplate LIKE '%RequesterDisplayName%'
)
BEGIN
    PRINT 'VIEWSET_DEPLOYMENT_SUCCESS already contains RequesterDisplayName — skipped.';
    RETURN;
END

-- ── Verify anchor placeholder exists ─────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey  = @TemplateKey
      AND BodyTemplate LIKE '%{RequesterBranch}%'
)
BEGIN
    PRINT 'WARNING: {RequesterBranch} not found in template — cannot determine insert position. Skipped.';
    RETURN;
END

-- ── STEP 1: Patch the greeting line ──────────────────────────────────────────
-- Replace <strong>{RequesterName}</strong> → <strong>{RequesterDisplayName}</strong>
-- so the greeting reads "Hi Mr. John Doe," when a title is set.

UPDATE dbo.EmailTemplate
SET BodyTemplate = REPLACE(
    BodyTemplate,
    '<strong>{RequesterName}</strong>',
    '<strong>{RequesterDisplayName}</strong>'
)
WHERE TemplateKey = @TemplateKey
  AND BodyTemplate LIKE '%<strong>{RequesterName}</strong>%';

PRINT 'VIEWSET_DEPLOYMENT_SUCCESS — greeting updated to use RequesterDisplayName.';

-- ── STEP 2: Add Title row to the Requester Details table ─────────────────────
-- Locate the <tr> that opens the Branch row ───────────────────────────────────
-- @chunk = everything before {RequesterBranch}, which includes the Branch row's
-- opening <tr> and its label <td>. Finding the LAST <tr> in @chunk gives us
-- the exact position to insert the new Title row before it.

SELECT @body = BodyTemplate
FROM dbo.EmailTemplate
WHERE TemplateKey = @TemplateKey;

SET @chunk  = LEFT(@body, CHARINDEX('{RequesterBranch}', @body) - 1);

-- Reverse-search for the last '<tr>' in @chunk.
-- Formula: position of '<' = LEN(@chunk) - CHARINDEX('>rt<', REVERSE(@chunk)) - 2
SET @revPos = CHARINDEX('>rt<', REVERSE(@chunk));
SET @trPos  = LEN(@chunk) - @revPos - 2;

-- ── Build the Title row using the same cell styles as the surrounding rows ───
SET @TitleRow =
    '<tr>'
    + '<td style="font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#374151; padding:8px 10px; background-color:#f9fafb; border:1px solid #e5e7eb; font-weight:600;">Title</td>'
    + '<td style="font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:8px 10px; border:1px solid #e5e7eb;">{RequesterTitle}</td>'
    + '</tr>' + CHAR(13) + CHAR(10);

-- ── Insert before the Branch <tr> ────────────────────────────────────────────
UPDATE dbo.EmailTemplate
SET BodyTemplate = STUFF(@body, @trPos, 0, @TitleRow)
WHERE TemplateKey = @TemplateKey;

-- ── Report result ─────────────────────────────────────────────────────────────
IF EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey  = @TemplateKey
      AND BodyTemplate LIKE '%RequesterTitle%'
      AND BodyTemplate LIKE '%RequesterDisplayName%'
)
    PRINT 'VIEWSET_DEPLOYMENT_SUCCESS updated — greeting and Title row both applied successfully.';
ELSE
    PRINT 'WARNING: Update may have failed — verify RequesterTitle and RequesterDisplayName are present.';
