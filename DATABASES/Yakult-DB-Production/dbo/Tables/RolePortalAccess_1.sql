CREATE TABLE [dbo].[RolePortalAccess] (
    [RoleId]   INT NOT NULL,
    [PortalId] INT NOT NULL,
    CONSTRAINT [PK_RolePortalAccess] PRIMARY KEY CLUSTERED ([RoleId] ASC, [PortalId] ASC),
    CONSTRAINT [FK_RolePortalAccess_Portal] FOREIGN KEY ([PortalId]) REFERENCES [dbo].[Portal] ([PortalId]),
    CONSTRAINT [FK_RolePortalAccess_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId])
);

