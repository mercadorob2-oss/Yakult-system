-- Diagnostic: Show the 400 characters around {RequesterBranch} in VIEWSET_DEPLOYMENT_SUCCESS
-- Run this to see the exact HTML pattern around the requester details table rows.

DECLARE @body    NVARCHAR(MAX);
DECLARE @pos     INT;
DECLARE @start   INT;
DECLARE @length  INT = 400;

SELECT @body = BodyTemplate
FROM dbo.EmailTemplate
WHERE TemplateKey = 'VIEWSET_DEPLOYMENT_SUCCESS';

IF @body IS NULL
BEGIN
    PRINT 'Template VIEWSET_DEPLOYMENT_SUCCESS not found.';
    RETURN;
END

SET @pos = CHARINDEX('RequesterBranch', @body);

IF @pos = 0
BEGIN
    PRINT '{RequesterBranch} placeholder not found in template body.';
    RETURN;
END

SET @start = @pos - 150;
IF @start < 1 SET @start = 1;

SELECT SUBSTRING(@body, @start, @length) AS Context_Around_RequesterBranch;
