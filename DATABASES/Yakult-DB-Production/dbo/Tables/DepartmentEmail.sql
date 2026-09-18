CREATE TABLE [dbo].[DepartmentEmail] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [CompanyName]    NVARCHAR (200) NOT NULL,
    [DepartmentName] NVARCHAR (200) NOT NULL,
    [EmailAddressId] INT            NULL,
    CONSTRAINT [PK_DepartmentEmail] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_DepartmentEmail_EmailAddress] FOREIGN KEY ([EmailAddressId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [UQ_DepartmentEmail_CompanyDept] UNIQUE NONCLUSTERED ([CompanyName] ASC, [DepartmentName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_DepartmentEmail_EmailLookup]
    ON [dbo].[DepartmentEmail]([CompanyName] ASC, [DepartmentName] ASC)
    INCLUDE([EmailAddressId]);

