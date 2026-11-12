-- Temporary desktop conversations. Each participant dismisses messages independently.
-- There is no timer-based expiry. Delete a message once both participants dismiss it.
IF OBJECT_ID('dbo.DesktopDeviceMessage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DesktopDeviceMessage (
        MessageId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DesktopDeviceMessage PRIMARY KEY,
        SenderMachine NVARCHAR(64) NOT NULL,
        SenderUser NVARCHAR(128) NOT NULL,
        SenderUserId INT NOT NULL,
        RecipientMachine NVARCHAR(64) NOT NULL,
        RecipientUser NVARCHAR(128) NOT NULL,
        Body NVARCHAR(1000) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DesktopDeviceMessage_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        SenderDismissedAtUtc DATETIME2(3) NULL,
        RecipientDismissedAtUtc DATETIME2(3) NULL,
        CONSTRAINT CK_DesktopDeviceMessage_Body CHECK (LEN(LTRIM(RTRIM(Body))) > 0)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DesktopDeviceMessage') AND name='IX_DesktopDeviceMessage_Recipient')
    CREATE INDEX IX_DesktopDeviceMessage_Recipient ON dbo.DesktopDeviceMessage(RecipientMachine,RecipientUser,MessageId) INCLUDE(SenderMachine,SenderUser,RecipientDismissedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DesktopDeviceMessage') AND name='IX_DesktopDeviceMessage_Sender')
    CREATE INDEX IX_DesktopDeviceMessage_Sender ON dbo.DesktopDeviceMessage(SenderMachine,SenderUser,MessageId) INCLUDE(RecipientMachine,RecipientUser,SenderDismissedAtUtc);
