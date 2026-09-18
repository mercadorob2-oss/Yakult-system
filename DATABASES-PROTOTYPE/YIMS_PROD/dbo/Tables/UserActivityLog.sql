CREATE TABLE [dbo].[UserActivityLog] (
    [ActivityId]  INT            IDENTITY (1, 1) NOT NULL,
    [UserId]      INT            NOT NULL,
    [ActionType]  NVARCHAR (50)  NOT NULL,
    [EntityType]  NVARCHAR (50)  NOT NULL,
    [EntityId]    INT            NULL,
    [Description] NVARCHAR (500) NULL,
    [CreatedDate] DATETIME2 (2)  CONSTRAINT [DF_UserActivityLog_CreatedDate] DEFAULT (sysutcdatetime()) NOT NULL,
    [IpAddress]   NVARCHAR (45)  NULL,
    [UserAgent]   NVARCHAR (255) NULL,
    PRIMARY KEY CLUSTERED ([ActivityId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_UserActivityLog_UserId_CreatedDate]
    ON [dbo].[UserActivityLog]([UserId] ASC, [CreatedDate] DESC)
    INCLUDE([ActionType], [EntityType], [EntityId], [Description]);


GO
CREATE NONCLUSTERED INDEX [IX_UserActivityLog_CreatedDate]
    ON [dbo].[UserActivityLog]([CreatedDate] DESC)
    INCLUDE([UserId], [ActionType], [EntityType], [EntityId]);


GO
CREATE NONCLUSTERED INDEX [IX_UserActivityLog_EntityType_ActionType]
    ON [dbo].[UserActivityLog]([EntityType] ASC, [ActionType] ASC)
    INCLUDE([UserId], [CreatedDate], [EntityId]);

