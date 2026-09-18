CREATE TABLE [dbo].[RolePermissionItem] (
    [RoleId]           INT      NOT NULL,
    [PermissionItemId] INT      NOT NULL,
    [IsGranted]        BIT      DEFAULT ((0)) NOT NULL,
    [DateModified]     DATETIME DEFAULT (getdate()) NOT NULL,
    [ModifiedByUserId] INT      NULL,
    PRIMARY KEY CLUSTERED ([RoleId] ASC, [PermissionItemId] ASC),
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId]),
    FOREIGN KEY ([PermissionItemId]) REFERENCES [dbo].[PermissionItem] ([PermissionItemId])
);
