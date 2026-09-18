CREATE TABLE [dbo].[User] (
    [UserId]              INT             IDENTITY (1, 1) NOT NULL,
    [Name]                NVARCHAR (100)  NOT NULL,
    [EmailAddress]        NVARCHAR (255)  NULL,
    [DateCreated]         DATETIME2 (2)   DEFAULT (sysutcdatetime()) NOT NULL,
    [RowVer]              ROWVERSION      NOT NULL,
    [Password]            VARBINARY (128) NULL,
    [IsDeveloper]         BIT             DEFAULT ((0)) NOT NULL,
    [EmpId]               INT             NULL,
    [PasswordHash]        VARBINARY (256) NULL,
    [PasswordSalt]        VARBINARY (128) NULL,
    [IsTemporaryPassword] BIT             DEFAULT ((0)) NOT NULL,
    [MustChangePassword]  BIT             DEFAULT ((0)) NOT NULL,
    [LastLoginDate]       DATETIME        CONSTRAINT [DF_User_LastLoginDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [IsActive]            BIT             DEFAULT ((1)) NOT NULL,
    PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_User_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    UNIQUE NONCLUSTERED ([EmailAddress] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Email]
    ON [dbo].[User]([EmailAddress] ASC) WHERE ([EmailAddress] IS NOT NULL);

