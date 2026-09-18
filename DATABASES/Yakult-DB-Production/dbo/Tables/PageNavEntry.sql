CREATE TABLE [dbo].[PageNavEntry] (
    [PageNavEntryId]   INT           IDENTITY (1, 1) NOT NULL,
    [PermissionItemId] INT           NOT NULL,
    [PortalKey]        VARCHAR (50)  NOT NULL,   -- matches dbo.Portal.PortalKey
    [MenuGroup]        VARCHAR (100) NOT NULL,   -- navbar section header, e.g. "Report Monitoring"
    [DisplayName]      VARCHAR (150) NOT NULL,   -- label shown at this nav entry point
    [SortOrder]        INT           DEFAULT ((0)) NOT NULL,
    [IsActive]         BIT           DEFAULT ((1)) NOT NULL,
    [DateCreated]      DATETIME      DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([PageNavEntryId] ASC),
    FOREIGN KEY ([PermissionItemId]) REFERENCES [dbo].[PermissionItem] ([PermissionItemId]),
    UNIQUE ([PermissionItemId], [PortalKey], [MenuGroup])
);
