CREATE TABLE [dbo].[BranchEmail] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [CompanyName]    NVARCHAR (200) NOT NULL,
    [BranchName]     NVARCHAR (200) NOT NULL,
    [EmailAddressId] INT            NULL,
    CONSTRAINT [PK_BranchEmail] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_BranchEmail_EmailAddress] FOREIGN KEY ([EmailAddressId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [UQ_BranchEmail_CompanyBranch] UNIQUE NONCLUSTERED ([CompanyName] ASC, [BranchName] ASC)
);

