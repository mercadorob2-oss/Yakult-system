CREATE TABLE [dbo].[CartridgeApprovalSignature] (
    [SignatureId]      INT             IDENTITY (1, 1) NOT NULL,
    [ApprovalId]       INT             NOT NULL,
    [SignatureType]    NVARCHAR (20)   NOT NULL,
    [SignaturePath]    NVARCHAR (500)  NULL,
    [SignatureData]    VARBINARY (MAX) NULL,
    [MimeType]         NVARCHAR (50)   NULL,
    [OriginalFileName] NVARCHAR (260)  NULL,
    [FileSizeBytes]    INT             NULL,
    [SignedAt]         DATETIME2 (2)   CONSTRAINT [DF_CAS_SignedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [SignedByUserId]   INT             NOT NULL,
    [IpAddress]        NVARCHAR (45)   NULL,
    CONSTRAINT [PK_CartridgeApprovalSignature] PRIMARY KEY CLUSTERED ([SignatureId] ASC),
    CONSTRAINT [CK_CAS_SignatureType] CHECK ([SignatureType]='Upload' OR [SignatureType]='Canvas'),
    CONSTRAINT [CK_CAS_StorageNotEmpty] CHECK ([SignaturePath] IS NOT NULL OR [SignatureData] IS NOT NULL),
    CONSTRAINT [FK_CAS_Approval] FOREIGN KEY ([ApprovalId]) REFERENCES [dbo].[CartridgeApproval] ([ApprovalId]),
    CONSTRAINT [FK_CAS_SignedBy] FOREIGN KEY ([SignedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_CAS_ApprovalId] UNIQUE NONCLUSTERED ([ApprovalId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CAS_ApprovalId]
    ON [dbo].[CartridgeApprovalSignature]([ApprovalId] ASC)
    INCLUDE([SignedAt], [SignatureType], [SignaturePath]);

