CREATE TABLE [dbo].[CartridgeModel] (
    [CartridgeModelId] INT            IDENTITY (1, 1) NOT NULL,
    [ModelNumber]      NVARCHAR (100) NOT NULL,
    [Brand]            NVARCHAR (100) NULL,
    [IsRequestable]    BIT            DEFAULT ((1)) NOT NULL,
    [IsRefillable]     BIT            DEFAULT ((1)) NOT NULL,
    [IsActive]         BIT            DEFAULT ((1)) NOT NULL,
    [CreatedAt]        DATETIME2 (7)  DEFAULT (sysdatetime()) NOT NULL,
    [CreatedBy]        INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([CartridgeModelId] ASC),
    UNIQUE NONCLUSTERED ([ModelNumber] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeModel_Requestable]
    ON [dbo].[CartridgeModel]([IsActive] ASC, [IsRequestable] ASC)
    INCLUDE([CartridgeModelId], [ModelNumber], [Brand]);

