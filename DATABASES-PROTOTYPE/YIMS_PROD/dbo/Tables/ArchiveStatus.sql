CREATE TABLE [dbo].[ArchiveStatus] (
    [ArchiveId]     INT            IDENTITY (1, 1) NOT NULL,
    [EntityType]    NVARCHAR (50)  NOT NULL,
    [EntityId]      INT            NOT NULL,
    [IsArchived]    BIT            DEFAULT ((1)) NOT NULL,
    [ArchivedAt]    DATETIME2 (7)  DEFAULT (sysdatetime()) NOT NULL,
    [ArchivedBy]    NVARCHAR (100) NOT NULL,
    [ArchiveReason] NVARCHAR (255) NULL,
    [RestoredAt]    DATETIME2 (7)  CONSTRAINT [DF_ArchiveStatus_RestoredAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RestoredBy]    NVARCHAR (100) NULL,
    CONSTRAINT [PK_ArchiveStatus] PRIMARY KEY CLUSTERED ([ArchiveId] ASC),
    CONSTRAINT [CK_ArchiveStatus_EntityType] CHECK ([EntityType]='EmptyCartridge' OR [EntityType]='CartridgeModel' OR [EntityType]='Item' OR [EntityType]='Inventory' OR [EntityType]='Request' OR [EntityType]='Set' OR [EntityType]='Company' OR [EntityType]='Department' OR [EntityType]='Branch' OR [EntityType]='Employee' OR [EntityType]='Vendor' OR [EntityType]='ItemCategory' OR [EntityType]='Condition' OR [EntityType]='Renewal'),
    CONSTRAINT [UQ_ArchiveStatus_Entity] UNIQUE NONCLUSTERED ([EntityType] ASC, [EntityId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ArchiveStatus_EntityType_IsArchived]
    ON [dbo].[ArchiveStatus]([EntityType] ASC, [IsArchived] ASC)
    INCLUDE([EntityId], [ArchivedAt], [ArchivedBy]);


GO
CREATE NONCLUSTERED INDEX [IX_ArchiveStatus_EntityId]
    ON [dbo].[ArchiveStatus]([EntityId] ASC) WHERE ([IsArchived]=(1));

