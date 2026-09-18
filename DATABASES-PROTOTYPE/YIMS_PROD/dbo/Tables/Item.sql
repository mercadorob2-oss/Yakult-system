CREATE TABLE [dbo].[Item] (
    [ItemId]            INT             IDENTITY (1, 1) NOT NULL,
    [Name]              NVARCHAR (200)  NOT NULL,
    [Description]       NVARCHAR (400)  NULL,
    [Active]            BIT             DEFAULT ((1)) NOT NULL,
    [UnitOfMeasure]     NVARCHAR (50)   NOT NULL,
    [StockOnHand]       INT             DEFAULT ((0)) NOT NULL,
    [DateCreated]       DATETIME2 (2)   DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]         INT             NOT NULL,
    [DateModified]      DATETIME2 (2)   CONSTRAINT [DF_Item_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]        INT             NOT NULL,
    [RowVer]            ROWVERSION      NOT NULL,
    [Category]          VARCHAR (100)   NULL,
    [SerialNumber]      VARCHAR (255)   NULL,
    [CategoryId]        INT             NOT NULL,
    [ModelNumber]       NVARCHAR (100)  NULL,
    [Amount]            DECIMAL (18, 2) DEFAULT ((0)) NOT NULL,
    [ItemType]          NVARCHAR (50)   NOT NULL,
    [StartDate]         DATETIME2 (7)   CONSTRAINT [DF_Item_StartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [EndDate]           DATETIME2 (7)   CONSTRAINT [DF_Item_EndDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ConditionID]       INT             DEFAULT ((1)) NOT NULL,
    [VendorId]          INT             NULL,
    [Remarks]           NVARCHAR (500)  NULL,
    [WarrantyYears]     INT             CONSTRAINT [DF_Item_WarrantyYears] DEFAULT ((0)) NOT NULL,
    [DurationYears]     INT             CONSTRAINT [DF_Item_DurationYears] DEFAULT ((5)) NOT NULL,
    [DurationStartDate] DATETIME2 (7)   CONSTRAINT [DF_Item_DurationStartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [DurationEndDate]   DATETIME2 (7)   CONSTRAINT [DF_Item_DurationEndDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [DatePurchased]     DATE            NULL,
    [LicenseNumber]     VARCHAR (50)    NULL,
    [WarrantyStartDate] DATETIME2 (7)   CONSTRAINT [DF_Item_WarrantyStartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [WarrantyEndDate]   AS              (dateadd(year,[WarrantyYears],[WarrantyStartDate])) PERSISTED,
    [AffectsInventory]  BIT             CONSTRAINT [DF_Item_AffectsInventory] DEFAULT ((1)) NOT NULL,
    [AcquisitionType]   NVARCHAR (50)   CONSTRAINT [DF_Item_AcquisitionType] DEFAULT ('Request') NULL,
    [IsTrackedAsset]    BIT             CONSTRAINT [DF_Item_IsTrackedAsset] DEFAULT ((0)) NOT NULL,
    [RefillStatus]      NVARCHAR (50)   NULL,
    [CartridgeModelId]  INT             NULL,
    [VendorBatchId]     INT             NULL,
    [IsPhysicalAsset]   BIT             CONSTRAINT [DF_Item_IsPhysicalAsset] DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([ItemId] ASC),
    CONSTRAINT [CK_Item_AcquisitionType] CHECK ([AcquisitionType]='Both' OR [AcquisitionType]='Invoice' OR [AcquisitionType]='Request'),
    CONSTRAINT [CK_Item_ItemType] CHECK ([ItemType]='Hardware' OR [ItemType]='Software/License' OR [ItemType]='Services'),
    CONSTRAINT [CK_Item_ModelNumber_By_Physical] CHECK ([IsPhysicalAsset]=(1) AND [ModelNumber] IS NOT NULL OR [IsPhysicalAsset]=(0)),
    CONSTRAINT [FK_Item_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId]),
    CONSTRAINT [FK_Item_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[ItemCategory] ([CategoryId]),
    CONSTRAINT [FK_Item_Condition] FOREIGN KEY ([ConditionID]) REFERENCES [dbo].[Condition] ([ConditionID]),
    CONSTRAINT [FK_Item_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Item_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Item_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [FK_Item_VendorCartridgeBatch] FOREIGN KEY ([VendorBatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_Item_SerialNumber]
    ON [dbo].[Item]([SerialNumber] ASC) WHERE ([SerialNumber] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Item_CategoryId]
    ON [dbo].[Item]([CategoryId] ASC)
    INCLUDE([Name], [ItemType], [Active]);


GO
CREATE NONCLUSTERED INDEX [IX_Item_ConditionId]
    ON [dbo].[Item]([ConditionID] ASC)
    INCLUDE([Name], [CategoryId], [Active]);


GO
CREATE NONCLUSTERED INDEX [IX_Item_VendorId]
    ON [dbo].[Item]([VendorId] ASC)
    INCLUDE([Name], [ItemType], [CategoryId]);


GO
CREATE NONCLUSTERED INDEX [IX_Item_ItemType]
    ON [dbo].[Item]([ItemType] ASC)
    INCLUDE([CategoryId], [Active], [Name]);


GO
CREATE NONCLUSTERED INDEX [IX_Item_Active]
    ON [dbo].[Item]([Active] ASC)
    INCLUDE([CategoryId], [ItemType], [StockOnHand]);


GO
CREATE NONCLUSTERED INDEX [IX_Item_CreatedBy]
    ON [dbo].[Item]([CreatedBy] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Item_WarrantyEndDate]
    ON [dbo].[Item]([WarrantyEndDate] ASC) WHERE ([WarrantyStartDate] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Item_DurationEndDate]
    ON [dbo].[Item]([DurationEndDate] ASC) WHERE ([DurationStartDate] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Item_RefillStatus]
    ON [dbo].[Item]([RefillStatus] ASC) WHERE ([RefillStatus] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Item_CartridgeModel_Available]
    ON [dbo].[Item]([CartridgeModelId] ASC)
    INCLUDE([StockOnHand]) WHERE ([Active]=(1) AND [AffectsInventory]=(1) AND [RefillStatus] IS NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Item_VendorBatchId]
    ON [dbo].[Item]([VendorBatchId] ASC)
    INCLUDE([Category], [RefillStatus], [Active], [ConditionID], [VendorId]) WHERE ([VendorBatchId] IS NOT NULL);


GO
CREATE   TRIGGER trg_Item_To_Cartridge
ON dbo.Item
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Exit early if no cartridge items were affected
    IF NOT EXISTS (
        SELECT 1 FROM inserted WHERE ItemType = 'Cartridge'
    )
        RETURN;

    ;WITH CartridgeItems AS (
        SELECT
            i.ItemId,
            CASE
                WHEN i.RefillStatus = 'Refill' THEN 2   -- Refill
                ELSE 1                                 -- Brand New
            END AS CartridgeTypeId
        FROM inserted i
        WHERE i.ItemType = 'Cartridge'
    )

    -- Insert missing Cartridge records
    INSERT INTO dbo.Cartridge (ItemId, CartridgeTypeId, IsActive)
    SELECT
        ci.ItemId,
        ci.CartridgeTypeId,
        1
    FROM CartridgeItems ci
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.Cartridge c
        WHERE c.ItemId = ci.ItemId
    );

    -- ONE-WAY transition: Brand New → Refill ONLY
    UPDATE c
    SET c.CartridgeTypeId = 2
    FROM dbo.Cartridge c
    JOIN CartridgeItems ci ON ci.ItemId = c.ItemId
    WHERE
        c.CartridgeTypeId = 1
        AND ci.CartridgeTypeId = 2;
END;
