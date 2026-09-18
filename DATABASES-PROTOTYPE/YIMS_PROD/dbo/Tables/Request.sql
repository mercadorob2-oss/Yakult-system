CREATE TABLE [dbo].[Request] (
    [ReqId]               INT              IDENTITY (1, 1) NOT NULL,
    [DateRequested]       DATETIME2 (2)    DEFAULT (sysutcdatetime()) NOT NULL,
    [Description]         NVARCHAR (400)   NOT NULL,
    [Remarks]             NVARCHAR (400)   NULL,
    [Status]              NVARCHAR (50)    NOT NULL,
    [EntryType]           NVARCHAR (50)    NOT NULL,
    [Quantity]            INT              NOT NULL,
    [DateCreated]         DATETIME2 (2)    DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]           INT              NOT NULL,
    [DateModified]        DATETIME2 (2)    CONSTRAINT [DF_Request_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]          INT              NOT NULL,
    [ItemId]              INT              NOT NULL,
    [EmpId]               INT              NOT NULL,
    [RowVer]              ROWVERSION       NOT NULL,
    [SetId]               INT              NULL,
    [UnitPrice]           DECIMAL (18, 2)  DEFAULT ((0)) NOT NULL,
    [Active]              BIT              CONSTRAINT [DF_Request_Active] DEFAULT ((1)) NOT NULL,
    [RequestSource]       VARCHAR (20)     DEFAULT ('INTERNAL') NOT NULL,
    [SubmissionSessionId] UNIQUEIDENTIFIER NULL,
    [ConditionID]         INT              NULL,
    PRIMARY KEY CLUSTERED ([ReqId] ASC),
    CONSTRAINT [FK_Request_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Request_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_Request_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_Request_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Request_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId]) ON DELETE SET NULL
);


GO
CREATE NONCLUSTERED INDEX [IX_Request_SetId]
    ON [dbo].[Request]([SetId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Request_EmpId]
    ON [dbo].[Request]([EmpId] ASC)
    INCLUDE([ItemId], [Quantity], [Status]);


GO
CREATE NONCLUSTERED INDEX [IX_Request_ItemId]
    ON [dbo].[Request]([ItemId] ASC)
    INCLUDE([EmpId], [Quantity], [SetId]);


GO
CREATE NONCLUSTERED INDEX [IX_Request_Status]
    ON [dbo].[Request]([Status] ASC)
    INCLUDE([SetId], [EmpId], [DateRequested]);


GO
CREATE NONCLUSTERED INDEX [IX_Request_CreatedBy]
    ON [dbo].[Request]([CreatedBy] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Request_SubmissionSessionId]
    ON [dbo].[Request]([SubmissionSessionId] ASC) WHERE ([SubmissionSessionId] IS NOT NULL);

