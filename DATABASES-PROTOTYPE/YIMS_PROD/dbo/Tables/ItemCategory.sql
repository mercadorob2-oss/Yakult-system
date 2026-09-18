CREATE TABLE [dbo].[ItemCategory] (
    [CategoryId]  INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50)  NOT NULL,
    [Description] NVARCHAR (200) NULL,
    [Active]      BIT            DEFAULT ((1)) NOT NULL,
    [DateCreated] DATETIME       DEFAULT (getdate()) NOT NULL,
    [CreatedBy]   INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([CategoryId] ASC),
    UNIQUE NONCLUSTERED ([Name] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemCategory_Active]
    ON [dbo].[ItemCategory]([Active] ASC) WHERE ([Active]=(1));

