CREATE TABLE [dbo].[EmailAddress] (
    [EmailId]         INT            IDENTITY (1, 1) NOT NULL,
    [EmailAddress]    NVARCHAR (255) NOT NULL,
    [DisplayName]     NVARCHAR (150) NULL,
    [IsActive]        BIT            CONSTRAINT [DF_EmailAddress_IsActive] DEFAULT ((1)) NOT NULL,
    [DateCreated]     DATETIME2 (2)  CONSTRAINT [DF_EmailAddress_DateCreated] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedByUserId] INT            NULL,
    CONSTRAINT [PK_EmailAddress] PRIMARY KEY CLUSTERED ([EmailId] ASC),
    CONSTRAINT [FK_EmailAddress_User] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_EmailAddress_Email] UNIQUE NONCLUSTERED ([EmailAddress] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_EmailAddress_EmailAddress]
    ON [dbo].[EmailAddress]([EmailAddress] ASC)
    INCLUDE([EmailId], [IsActive]);


GO
CREATE NONCLUSTERED INDEX [IX_EmailAddress_IsActive]
    ON [dbo].[EmailAddress]([IsActive] ASC)
    INCLUDE([EmailId], [EmailAddress]);

