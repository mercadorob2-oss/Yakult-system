CREATE TABLE [dbo].[Title] (
    [TitleId]     INT           IDENTITY (1, 1) NOT NULL,
    [Code]        NVARCHAR (10) NOT NULL,
    [Description] NVARCHAR (50) NOT NULL,
    CONSTRAINT [PK_Title] PRIMARY KEY CLUSTERED ([TitleId] ASC),
    CONSTRAINT [UQ_Title_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);

