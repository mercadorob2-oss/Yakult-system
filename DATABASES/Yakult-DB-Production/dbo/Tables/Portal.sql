CREATE TABLE [dbo].[Portal] (
    [PortalId]    INT           IDENTITY (1, 1) NOT NULL,
    [PortalKey]   VARCHAR (50)  NOT NULL,   -- matches C# PermissionResolver.Portal enum name
    [DisplayName] VARCHAR (100) NOT NULL,
    [IsActive]    BIT           DEFAULT ((1)) NOT NULL,
    [DateCreated] DATETIME      DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([PortalId] ASC),
    UNIQUE ([PortalKey])
);
