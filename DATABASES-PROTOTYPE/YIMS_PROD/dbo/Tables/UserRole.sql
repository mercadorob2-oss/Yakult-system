CREATE TABLE [dbo].[UserRole] (
    [UserId]       INT      NOT NULL,
    [RoleId]       INT      NOT NULL,
    [DateAssigned] DATETIME DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
    CONSTRAINT [FK_UserRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId]),
    CONSTRAINT [FK_UserRole_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId])
);

