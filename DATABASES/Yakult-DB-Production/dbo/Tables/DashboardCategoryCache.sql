CREATE TABLE [dbo].[DashboardCategoryCache] (
    [CacheId]          INT            IDENTITY (1, 1) NOT NULL,
    [CategoryId]       INT            NULL,
    [CategoryName]     NVARCHAR (255) NOT NULL,
    [Description]      NVARCHAR (500) NULL,
    [CategoryActive]   BIT            NULL,
    [TotalStock]       INT            DEFAULT ((0)) NOT NULL,
    [TotalItems_All]   INT            DEFAULT ((0)) NOT NULL,
    [TotalActiveItems] INT            DEFAULT ((0)) NOT NULL,
    [ActiveStock]      INT            DEFAULT ((0)) NOT NULL,
    [SerializedItems]  INT            DEFAULT ((0)) NOT NULL,
    [GoodCount]        INT            DEFAULT ((0)) NOT NULL,
    [DamagedCount]     INT            DEFAULT ((0)) NOT NULL,
    [LastRefreshed]    DATETIME       DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([CacheId] ASC)
);

