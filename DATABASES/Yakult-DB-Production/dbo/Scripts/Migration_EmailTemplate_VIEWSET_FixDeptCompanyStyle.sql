-- Migration: Fix Department and Company row styling in VIEWSET_DEPLOYMENT_SUCCESS
--
-- The value <td> cells for Department and Company were added without inline styles,
-- and the label cells have trailing colons. This script aligns them with the
-- Full Name / Branch / Email rows in the same Requester Details table.
--
-- Safe to run multiple times: each REPLACE is idempotent.

DECLARE @key NVARCHAR(100) = 'VIEWSET_DEPLOYMENT_SUCCESS';

-- ── Fix unstyled Department value cell ───────────────────────────────────────
UPDATE dbo.EmailTemplate
SET BodyTemplate = REPLACE(
    BodyTemplate,
    '<td>{RequesterDepartment}</td>',
    '<td style="font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:8px 10px; border:1px solid #e5e7eb;">{RequesterDepartment}</td>'
)
WHERE TemplateKey = @key
  AND BodyTemplate LIKE '%<td>{RequesterDepartment}</td>%';

-- ── Fix unstyled Company value cell ──────────────────────────────────────────
UPDATE dbo.EmailTemplate
SET BodyTemplate = REPLACE(
    BodyTemplate,
    '<td>{RequesterCompany}</td>',
    '<td style="font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#111827; padding:8px 10px; border:1px solid #e5e7eb;">{RequesterCompany}</td>'
)
WHERE TemplateKey = @key
  AND BodyTemplate LIKE '%<td>{RequesterCompany}</td>%';

-- ── Remove trailing colon from Department label ───────────────────────────────
UPDATE dbo.EmailTemplate
SET BodyTemplate = REPLACE(BodyTemplate, '>Department:</td>', '>Department</td>')
WHERE TemplateKey = @key
  AND BodyTemplate LIKE '%>Department:</td>%';

-- ── Remove trailing colon from Company label ─────────────────────────────────
UPDATE dbo.EmailTemplate
SET BodyTemplate = REPLACE(BodyTemplate, '>Company:</td>', '>Company</td>')
WHERE TemplateKey = @key
  AND BodyTemplate LIKE '%>Company:</td>%';

PRINT 'VIEWSET_DEPLOYMENT_SUCCESS style fixes applied.';
