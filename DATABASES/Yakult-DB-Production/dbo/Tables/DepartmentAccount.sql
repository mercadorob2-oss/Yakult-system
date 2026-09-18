CREATE TABLE [dbo].[DepartmentAccount] (
    [Id]             INT             IDENTITY (1, 1) NOT NULL,
    [CompanyName]    NVARCHAR (200)  NOT NULL,
    [DepartmentName] NVARCHAR (200)  NOT NULL,
    [BranchName]     NVARCHAR (200)  NOT NULL,
    [Username]       NVARCHAR (200)  NULL,
    [PasswordHash]   VARBINARY (MAX) NULL,
    [PasswordSalt]   VARBINARY (MAX) NULL,
    [IsActive]       BIT             NULL,
    [DateCreated]    DATETIME        NULL,
    [UserId]         INT             NULL,
    [EmailAddressId] INT             NULL,
    [PlainPassword]  NVARCHAR (200)  NULL,
    CONSTRAINT [PK_DepartmentAccount] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_DepartmentAccount_EmailAddress] FOREIGN KEY ([EmailAddressId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [FK_DepartmentAccount_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_DepartmentAccount_Combo] UNIQUE NONCLUSTERED ([CompanyName] ASC, [DepartmentName] ASC, [BranchName] ASC)
);




GO
CREATE NONCLUSTERED INDEX [IX_DepartmentAccount_EmailLookup]
    ON [dbo].[DepartmentAccount]([CompanyName] ASC, [DepartmentName] ASC, [BranchName] ASC)
    INCLUDE([EmailAddressId]);

