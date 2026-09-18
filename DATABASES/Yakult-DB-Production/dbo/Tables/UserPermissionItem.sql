CREATE TABLE [dbo].[UserPermissionItem] (
    [UserId]           INT      NOT NULL,
    [PermissionItemId] INT      NOT NULL,
    [IsGranted]        BIT      DEFAULT ((0)) NOT NULL,
    [DateModified]     DATETIME DEFAULT (getdate()) NOT NULL,
    [ModifiedByUserId] INT      NULL,
    PRIMARY KEY CLUSTERED ([UserId] ASC, [PermissionItemId] ASC),
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]),
    FOREIGN KEY ([PermissionItemId]) REFERENCES [dbo].[PermissionItem] ([PermissionItemId])
);
