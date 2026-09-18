CREATE TABLE [dbo].[CallEmailTemplate] (
    [TemplateId]      INT            IDENTITY (1, 1) NOT NULL,
    [TemplateType]    NVARCHAR (30)  NOT NULL,
    [Subject]         NVARCHAR (255) NOT NULL,
    [Body]            NVARCHAR (MAX) NOT NULL,
    [IsActive]        BIT            CONSTRAINT [DF_CallEmailTemplate_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]       DATETIME2 (2)  CONSTRAINT [DF_CallEmailTemplate_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT            NULL,
    CONSTRAINT [PK_CallEmailTemplate] PRIMARY KEY CLUSTERED ([TemplateId] ASC),
    CONSTRAINT [FK_CallEmailTemplate_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_CallEmailTemplate_Type] UNIQUE NONCLUSTERED ([TemplateType] ASC)
);

