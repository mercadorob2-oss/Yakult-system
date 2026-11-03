-- Add explicit Active Directory identity mapping to existing portal user accounts.
IF COL_LENGTH('dbo.[User]', 'AdAccountSid') IS NULL
BEGIN
    ALTER TABLE dbo.[User] ADD AdAccountSid NVARCHAR(184) NULL;
END;

IF COL_LENGTH('dbo.[User]', 'AdAccountName') IS NULL
BEGIN
    ALTER TABLE dbo.[User] ADD AdAccountName NVARCHAR(256) NULL;
END;

GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[User]') AND name = 'UX_User_AdAccountSid'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_User_AdAccountSid
        ON dbo.[User](AdAccountSid)
        WHERE AdAccountSid IS NOT NULL;
END;
