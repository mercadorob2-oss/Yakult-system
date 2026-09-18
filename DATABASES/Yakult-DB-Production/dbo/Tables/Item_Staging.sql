CREATE TABLE [dbo].[Item_Staging] (
    [StagingId]    INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]       INT            NULL,
    [Name]         NVARCHAR (200) NULL,
    [Description]  NVARCHAR (400) NULL,
    [ModelNumber]  NVARCHAR (500) NULL,
    [SerialNumber] VARCHAR (255)  NULL,
    [ImportedDate] DATETIME2 (2)  DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([StagingId] ASC)
);

