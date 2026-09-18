CREATE TABLE [dbo].[SetImages] (
    [ImageId]          INT             IDENTITY (1, 1) NOT NULL,
    [SetId]            INT             NULL,
    [ImagePath]        VARCHAR (500)   NULL,
    [ImageType]        VARCHAR (50)    DEFAULT ('Photo') NULL,
    [UploadedBy]       VARCHAR (100)   NULL,
    [UploadDate]       DATETIME        DEFAULT (getdate()) NULL,
    [ImageData]        VARBINARY (MAX) NULL,
    [MimeType]         NVARCHAR (50)   NULL,
    [OriginalFileName] NVARCHAR (260)  NULL,
    [FileSizeBytes]    INT             NULL,
    PRIMARY KEY CLUSTERED ([ImageId] ASC),
    CONSTRAINT [CK_SetImages_StorageNotEmpty] CHECK ([ImagePath] IS NOT NULL OR [ImageData] IS NOT NULL),
    FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId])
);


GO
CREATE NONCLUSTERED INDEX [IX_SetImages_SetId]
    ON [dbo].[SetImages]([SetId] ASC);

