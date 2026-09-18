CREATE TABLE [dbo].[Role] (
    [RoleId]      INT           IDENTITY (1, 1) NOT NULL,
    [RoleName]    VARCHAR (50)  NOT NULL,
    [Description] VARCHAR (255) NULL,
    [IsActive]    BIT           DEFAULT ((1)) NOT NULL,
    [DateCreated] DATETIME      DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([RoleId] ASC)
);

