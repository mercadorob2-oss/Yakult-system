CREATE TABLE [dbo].[Branch] (
    [BranchId]      INT            IDENTITY (1, 1) NOT NULL,
    [Name]          NVARCHAR (150) NOT NULL,
    [Description]   NVARCHAR (400) NULL,
    [DateCreated]   DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]     INT            NOT NULL,
    [DateModified]  DATETIME2 (2)  CONSTRAINT [DF_Branch_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]    INT            NULL,
    [ComId]         INT            NULL,
    [DeptId]        INT            NULL,
    [RowVer]        ROWVERSION     NOT NULL,
    [IsFactory]     BIT            DEFAULT ((0)) NOT NULL,
    [IsDepot]       BIT            DEFAULT ((0)) NOT NULL,
    [IsDistributor] BIT            DEFAULT ((0)) NOT NULL,
    [IsCenter]      BIT            DEFAULT ((0)) NOT NULL,
    [CenterRegion]  NVARCHAR (50)  NULL,
    [Active]        BIT            DEFAULT ((1)) NOT NULL,
    [EmailId]       INT            NULL,
    PRIMARY KEY CLUSTERED ([BranchId] ASC),
    CONSTRAINT [FK_Branch_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_Branch_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Branch_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_Branch_Email] FOREIGN KEY ([EmailId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [FK_Branch_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Branch_Active]
    ON [dbo].[Branch]([Active] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Branch_ComId]
    ON [dbo].[Branch]([ComId] ASC)
    INCLUDE([Name], [Active]);


GO
CREATE NONCLUSTERED INDEX [IX_Branch_DeptId]
    ON [dbo].[Branch]([DeptId] ASC)
    INCLUDE([Name], [ComId], [Active]);


GO
CREATE NONCLUSTERED INDEX [IX_Branch_EmailId]
    ON [dbo].[Branch]([EmailId] ASC)
    INCLUDE([BranchId]);

