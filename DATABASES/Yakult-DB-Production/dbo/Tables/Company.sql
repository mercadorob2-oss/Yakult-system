CREATE TABLE [dbo].[Company] (
    [ComId]        INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (150) NOT NULL,
    [Description]  NVARCHAR (400) NULL,
    [DateCreated]  DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]    INT            NULL,
    [DateModified] DATETIME2 (2)  CONSTRAINT [DF_Company_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]   INT            NULL,
    [RowVer]       ROWVERSION     NOT NULL,
    [Active]       BIT            DEFAULT ((1)) NOT NULL,
    [Acronym]      NVARCHAR (20)  NULL,
    PRIMARY KEY CLUSTERED ([ComId] ASC),
    CONSTRAINT [FK_Company_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Company_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Company_Active]
    ON [dbo].[Company]([Active] ASC);

