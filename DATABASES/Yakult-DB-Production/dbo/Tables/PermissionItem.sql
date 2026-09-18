CREATE TABLE [dbo].[PermissionItem] (
    [PermissionItemId] INT           IDENTITY (1, 1) NOT NULL,
    [PermissionType]    VARCHAR (30)  NOT NULL,   -- 'Page' | 'ItemCategory' | 'Action' | future types
    [ItemKey]           VARCHAR (100) NOT NULL,   -- e.g. 'ViewInvoicePage', 'Ink', 'Item.Add'
    [DisplayName]       VARCHAR (150) NOT NULL,
    [IsActive]          BIT           DEFAULT ((1)) NOT NULL,
    [DateCreated]       DATETIME      DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([PermissionItemId] ASC),
    UNIQUE ([PermissionType], [ItemKey])
);
