CREATE TABLE [dbo].[AccountLevel] (
    [LevelId]   INT           IDENTITY (1, 1) NOT NULL,
    [LevelName] NVARCHAR (50) NOT NULL,
    [LevelRank] INT           NOT NULL,
    CONSTRAINT [PK_AccountLevel] PRIMARY KEY CLUSTERED ([LevelId] ASC),
    CONSTRAINT [UQ_AccountLevel_LevelName] UNIQUE NONCLUSTERED ([LevelName] ASC),
    CONSTRAINT [UQ_AccountLevel_LevelRank] UNIQUE NONCLUSTERED ([LevelRank] ASC)
);

