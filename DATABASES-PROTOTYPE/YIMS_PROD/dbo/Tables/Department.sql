CREATE TABLE [dbo].[Department] (
    [DeptId]       INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (150) NOT NULL,
    [Description]  NVARCHAR (400) NULL,
    [DateCreated]  DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]    INT            NOT NULL,
    [DateModified] DATETIME2 (2)  CONSTRAINT [DF_Department_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]   INT            NULL,
    [ComId]        INT            NULL,
    [RowVer]       ROWVERSION     NOT NULL,
    [Active]       BIT            DEFAULT ((1)) NOT NULL,
    PRIMARY KEY CLUSTERED ([DeptId] ASC),
    CONSTRAINT [FK_Department_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_Department_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Department_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Department_Active]
    ON [dbo].[Department]([Active] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Department_ComId]
    ON [dbo].[Department]([ComId] ASC)
    INCLUDE([Name], [Active]);

