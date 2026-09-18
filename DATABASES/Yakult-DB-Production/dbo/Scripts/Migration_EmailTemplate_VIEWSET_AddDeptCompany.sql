-- Migration: Add Department and Company rows to VIEWSET_DEPLOYMENT_SUCCESS email template
--
-- Appends {RequesterDepartment} and {RequesterCompany} rows immediately after the
-- {RequesterBranch} row in the Requester Details section.
--
-- Supports both {Placeholder} and {{Placeholder}} template syntax.
-- Safe to run multiple times: skips if RequesterDepartment is already present.
-- If the auto-patch cannot match the pattern, it prints a manual fallback message.

DECLARE @TemplateKey NVARCHAR(100) = 'VIEWSET_DEPLOYMENT_SUCCESS';

DECLARE @DeptRow NVARCHAR(512) =
    '<tr><td style="padding:8px 0; color:#666;"><strong>Department:</strong></td>'
    + '<td style="padding:8px 0;">';
DECLARE @CompRow NVARCHAR(512) =
    '<tr><td style="padding:8px 0; color:#666;"><strong>Company:</strong></td>'
    + '<td style="padding:8px 0;">';

-- ── Verify the template exists ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE TemplateKey = @TemplateKey)
BEGIN
    PRINT 'Template VIEWSET_DEPLOYMENT_SUCCESS not found — skipped.';
    RETURN;
END

-- ── Skip if already migrated ─────────────────────────────────────────────────
IF EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey  = @TemplateKey
      AND BodyTemplate LIKE '%RequesterDepartment%'
)
BEGIN
    PRINT 'VIEWSET_DEPLOYMENT_SUCCESS already contains RequesterDepartment — skipped.';
    RETURN;
END

-- ── Apply the patch ───────────────────────────────────────────────────────────
-- Replace "{RequesterBranch}</td></tr>" with itself followed by the two new rows.
-- The new rows share the same inline style as the rest of the requester table.

UPDATE dbo.EmailTemplate
SET BodyTemplate = CASE

    -- Single-brace syntax
    WHEN BodyTemplate LIKE '%{RequesterBranch}</td></tr>%'
    THEN REPLACE(
        BodyTemplate,
        '{RequesterBranch}</td></tr>',
        '{RequesterBranch}</td></tr>' + CHAR(13)+CHAR(10)
        + @DeptRow + '{RequesterDepartment}</td></tr>' + CHAR(13)+CHAR(10)
        + @CompRow + '{RequesterCompany}</td></tr>'
    )

    -- Double-brace syntax
    WHEN BodyTemplate LIKE '%{{RequesterBranch}}</td></tr>%'
    THEN REPLACE(
        BodyTemplate,
        '{{RequesterBranch}}</td></tr>',
        '{{RequesterBranch}}</td></tr>' + CHAR(13)+CHAR(10)
        + @DeptRow + '{{RequesterDepartment}}</td></tr>' + CHAR(13)+CHAR(10)
        + @CompRow + '{{RequesterCompany}}</td></tr>'
    )

    ELSE BodyTemplate  -- pattern not found; leave unchanged
END
WHERE TemplateKey = @TemplateKey;

-- ── Report result ─────────────────────────────────────────────────────────────
IF EXISTS (
    SELECT 1 FROM dbo.EmailTemplate
    WHERE TemplateKey  = @TemplateKey
      AND BodyTemplate LIKE '%RequesterDepartment%'
)
BEGIN
    PRINT 'VIEWSET_DEPLOYMENT_SUCCESS updated — Department and Company rows added.';
END
ELSE
BEGIN
    PRINT 'WARNING: Auto-patch did not match. The {RequesterBranch}</td></tr> pattern was not found.';
    PRINT 'Manually add these rows to the Requester Details table in the Email Template editor:';
    PRINT '  <tr><td>Department:</td><td>{RequesterDepartment}</td></tr>';
    PRINT '  <tr><td>Company:</td>   <td>{RequesterCompany}</td></tr>';
END
