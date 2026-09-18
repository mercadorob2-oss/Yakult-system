CREATE TABLE [dbo].[AuditTrail] (
    [Id]         INT            IDENTITY (1, 1) NOT NULL,
    [Action]     NVARCHAR (100) NOT NULL,
    [EntityId]   INT            NULL,
    [EntityType] NVARCHAR (100) NULL,
    [UserId]     INT            NULL,
    [UserName]   NVARCHAR (255) NULL,
    [Timestamp]  DATETIME2 (7)  CONSTRAINT [DF_AuditTrail_Timestamp] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NOT NULL,
    [Notes]      NVARCHAR (MAX) NULL,
    [OldValues]  NVARCHAR (MAX) NULL,
    [NewValues]  NVARCHAR (MAX) NULL,
    [IpAddress]  NVARCHAR (50)  NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_AuditTrail_Timestamp]
    ON [dbo].[AuditTrail]([Timestamp] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AuditTrail_Entity]
    ON [dbo].[AuditTrail]([EntityType] ASC, [EntityId] ASC)
    INCLUDE([Action], [UserId], [UserName], [Timestamp]);


GO
CREATE NONCLUSTERED INDEX [IX_AuditTrail_User]
    ON [dbo].[AuditTrail]([UserId] ASC)
    INCLUDE([Action], [EntityType], [EntityId], [Timestamp]);

