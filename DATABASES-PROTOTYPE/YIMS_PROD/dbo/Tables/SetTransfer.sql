CREATE TABLE [dbo].[SetTransfer] (
    [SetTransferId]    INT            IDENTITY (1, 1) NOT NULL,
    [SetId]            INT            NOT NULL,
    [FromBranchId]     INT            NOT NULL,
    [ToBranchId]       INT            NOT NULL,
    [FromDepartmentId] INT            NULL,
    [ToDepartmentId]   INT            NULL,
    [TransferReason]   NVARCHAR (500) NOT NULL,
    [TransferDate]     DATETIME2 (2)  CONSTRAINT [DF_SetTransfer_TransferDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RequestedBy]      INT            NOT NULL,
    [ApprovedBy]       INT            NULL,
    [Status]           NVARCHAR (20)  DEFAULT ('Pending') NOT NULL,
    [DateCreated]      DATETIME2 (2)  DEFAULT (getdate()) NOT NULL,
    [CreatedBy]        INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([SetTransferId] ASC),
    CONSTRAINT [CK_SetTransfer_Status] CHECK ([Status]='Rejected' OR [Status]='Completed' OR [Status]='Approved' OR [Status]='Pending'),
    CONSTRAINT [FK_SetTransfer_FromBranch] FOREIGN KEY ([FromBranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_SetTransfer_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId]),
    CONSTRAINT [FK_SetTransfer_ToBranch] FOREIGN KEY ([ToBranchId]) REFERENCES [dbo].[Branch] ([BranchId])
);


GO
CREATE NONCLUSTERED INDEX [IX_SetTransfer_SetId]
    ON [dbo].[SetTransfer]([SetId] ASC)
    INCLUDE([Status], [TransferDate], [FromBranchId], [ToBranchId]);


GO
CREATE NONCLUSTERED INDEX [IX_SetTransfer_Status]
    ON [dbo].[SetTransfer]([Status] ASC)
    INCLUDE([SetId], [TransferDate]);


GO
CREATE NONCLUSTERED INDEX [IX_SetTransfer_FromBranchId]
    ON [dbo].[SetTransfer]([FromBranchId] ASC)
    INCLUDE([ToBranchId], [SetId], [Status]);


GO
CREATE NONCLUSTERED INDEX [IX_SetTransfer_ToBranchId]
    ON [dbo].[SetTransfer]([ToBranchId] ASC)
    INCLUDE([FromBranchId], [SetId], [Status]);

