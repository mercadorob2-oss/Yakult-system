CREATE TABLE [dbo].[Set] (
    [SetId]               INT              IDENTITY (1, 1) NOT NULL,
    [SetCode]             AS               (concat('SET-',right('0000'+CONVERT([varchar](4),[SetId]),(4)))) PERSISTED NOT NULL,
    [CreatedBy]           INT              NOT NULL,
    [CreatedAt]           DATETIME2 (7)    DEFAULT (sysutcdatetime()) NOT NULL,
    [QRToken]             UNIQUEIDENTIFIER DEFAULT (newid()) NULL,
    [QRImagePath]         NVARCHAR (400)   NULL,
    [QRImageData]         VARBINARY (MAX)  NULL,
    [Remarks]             NVARCHAR (400)   NULL,
    [DispatchDate]        DATETIME2 (7)    CONSTRAINT [DF_Set_DispatchDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [SetType]             NVARCHAR (50)    DEFAULT ('Pending') NOT NULL,
    [QRData]              NVARCHAR (MAX)   NULL,
    [Subtotal]            DECIMAL (18, 2)  DEFAULT ((0)) NULL,
    [VatAmount]           DECIMAL (18, 2)  DEFAULT ((0)) NULL,
    [WhtAmount]           DECIMAL (18, 2)  DEFAULT ((0)) NULL,
    [DiscountAmount]      DECIMAL (18, 2)  DEFAULT ((0)) NULL,
    [TotalAmountDue]      DECIMAL (18, 2)  DEFAULT ((0)) NULL,
    [DocumentNumber]      NVARCHAR (100)   NULL,
    [ReferenceNumber]     NVARCHAR (100)   NULL,
    [Status]              NVARCHAR (20)    NULL,
    [Site]                NVARCHAR (100)   NULL,
    [ComId]               INT              NULL,
    [StartDate]           DATETIME2 (7)    CONSTRAINT [DF_Set_StartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [EndDate]             DATETIME2 (7)    CONSTRAINT [DF_Set_EndDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ReqId]               INT              NULL,
    [CurrentBranchId]     INT              NULL,
    [CurrentDepartmentId] INT              NULL,
    [Active]              BIT              CONSTRAINT [DF_Set_Active] DEFAULT ((1)) NOT NULL,
    [UpgradeReason]       NVARCHAR (500)   NULL,
    [VendorId]            INT              NULL,
    [ComputerName]        NVARCHAR (150)   NULL,
    [IPAddress]           NVARCHAR (100)   NULL,
    [IsInvoice]           BIT              NULL,
    [IssuedBrandNewQty]   INT              DEFAULT ((0)) NOT NULL,
    [IssuedRefilledQty]   INT              DEFAULT ((0)) NOT NULL,
    [RenewalOfSetId]      INT              NULL,
    PRIMARY KEY CLUSTERED ([SetId] ASC),
    CONSTRAINT [CK_Set_IsInvoice_Valid] CHECK ([IsInvoice]=(1) OR [IsInvoice]=(0) OR [IsInvoice] IS NULL),
    CONSTRAINT [FK_Set_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_Set_CurrentBranch] FOREIGN KEY ([CurrentBranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_Set_CurrentDepartment] FOREIGN KEY ([CurrentDepartmentId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_Set_Request] FOREIGN KEY ([ReqId]) REFERENCES [dbo].[Request] ([ReqId]),
    CONSTRAINT [FK_Set_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [FK_Set_RenewalOfSet] FOREIGN KEY ([RenewalOfSetId]) REFERENCES [dbo].[Set] ([SetId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Set_ReqId]
    ON [dbo].[Set]([ReqId] ASC)
    INCLUDE([SetCode], [SetType], [Status]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_SetType]
    ON [dbo].[Set]([SetType] ASC)
    INCLUDE([Status], [DispatchDate], [SetCode]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_Status]
    ON [dbo].[Set]([Status] ASC)
    INCLUDE([SetType], [SetCode], [DispatchDate]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_ComId]
    ON [dbo].[Set]([ComId] ASC)
    INCLUDE([SetCode], [SetType], [Status]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_CurrentBranchId]
    ON [dbo].[Set]([CurrentBranchId] ASC)
    INCLUDE([CurrentDepartmentId], [SetCode]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_CurrentDepartmentId]
    ON [dbo].[Set]([CurrentDepartmentId] ASC)
    INCLUDE([CurrentBranchId], [SetCode]);


GO
CREATE NONCLUSTERED INDEX [IX_Set_CreatedBy]
    ON [dbo].[Set]([CreatedBy] ASC);

