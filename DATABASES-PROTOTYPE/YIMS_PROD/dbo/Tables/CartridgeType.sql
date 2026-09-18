CREATE TABLE [dbo].[CartridgeType] (
    [CartridgeTypeId]   INT          IDENTITY (1, 1) NOT NULL,
    [CartridgeTypeName] VARCHAR (20) NOT NULL,
    PRIMARY KEY CLUSTERED ([CartridgeTypeId] ASC),
    UNIQUE NONCLUSTERED ([CartridgeTypeName] ASC)
);

