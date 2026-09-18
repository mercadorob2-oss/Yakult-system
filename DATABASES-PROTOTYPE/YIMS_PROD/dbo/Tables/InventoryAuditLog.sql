CREATE TABLE [dbo].[InventoryAuditLog] (
    [AuditId]      INT            IDENTITY (1, 1) NOT NULL,
    [InvId]        INT            NULL,
    [Action]       NVARCHAR (50)  NOT NULL,
    [SetId]        INT            NULL,
    [ItemId]       INT            NULL,
    [ReqId]        INT            NULL,
    [ErrorMessage] NVARCHAR (MAX) NULL,
    [AuditDate]    DATETIME2 (7)  DEFAULT (getdate()) NOT NULL,
    [AuditUser]    NVARCHAR (100) DEFAULT (suser_sname()) NOT NULL,
    PRIMARY KEY CLUSTERED ([AuditId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_InventoryAuditLog_AuditDate]
    ON [dbo].[InventoryAuditLog]([AuditDate] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_InventoryAuditLog_InvId]
    ON [dbo].[InventoryAuditLog]([InvId] ASC)
    INCLUDE([Action], [SetId], [ItemId], [ReqId], [AuditDate], [AuditUser]);


GO
CREATE NONCLUSTERED INDEX [IX_InventoryAuditLog_SetId]
    ON [dbo].[InventoryAuditLog]([SetId] ASC)
    INCLUDE([Action], [InvId], [ItemId], [AuditDate]);

