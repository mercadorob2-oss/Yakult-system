SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Role', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[Role] WHERE RoleName = 'EmployeeResourceEditor')
        INSERT dbo.[Role](RoleName, Description, IsActive, DateCreated)
        VALUES ('EmployeeResourceEditor', 'Creates and maintains Employee Resources drafts and attachments.', 1, GETDATE());

    IF NOT EXISTS (SELECT 1 FROM dbo.[Role] WHERE RoleName = 'EmployeeResourcePublisher')
        INSERT dbo.[Role](RoleName, Description, IsActive, DateCreated)
        VALUES ('EmployeeResourcePublisher', 'Publishes and archives Employee Resources.', 1, GETDATE());
END;

IF OBJECT_ID(N'dbo.EmployeeResourceCategory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeResourceCategory
    (
        CategoryId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeResourceCategory PRIMARY KEY,
        Name        NVARCHAR(100) NOT NULL,
        Slug        VARCHAR(100) NOT NULL CONSTRAINT UQ_EmployeeResourceCategory_Slug UNIQUE,
        Description NVARCHAR(500) NULL,
        SortOrder   INT NOT NULL CONSTRAINT DF_EmployeeResourceCategory_SortOrder DEFAULT (0),
        IsActive    BIT NOT NULL CONSTRAINT DF_EmployeeResourceCategory_IsActive DEFAULT (1)
    );
END;

IF OBJECT_ID(N'dbo.EmployeeResource', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeResource
    (
        ResourceId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeResource PRIMARY KEY,
        Slug                 VARCHAR(200) NOT NULL CONSTRAINT UQ_EmployeeResource_Slug UNIQUE,
        Title                NVARCHAR(180) NOT NULL,
        Summary              NVARCHAR(500) NOT NULL CONSTRAINT DF_EmployeeResource_Summary DEFAULT (N''),
        Overview             NVARCHAR(MAX) NOT NULL CONSTRAINT DF_EmployeeResource_Overview DEFAULT (N''),
        ResourceType         VARCHAR(50) NOT NULL,
        CategoryId           INT NOT NULL,
        OwnerDepartmentId    INT NULL,
        Version              NVARCHAR(20) NOT NULL CONSTRAINT DF_EmployeeResource_Version DEFAULT (N'v1.0'),
        Status               VARCHAR(30) NOT NULL CONSTRAINT DF_EmployeeResource_Status DEFAULT ('Draft'),
        PublishStartUtc      DATETIME2(2) NULL,
        PublishEndUtc        DATETIME2(2) NULL,
        NextReviewDateUtc    DATETIME2(2) NULL,
        AuthorUserId         INT NOT NULL,
        ApprovedByUserId     INT NULL,
        ApprovedAtUtc        DATETIME2(2) NULL,
        CreatedAtUtc         DATETIME2(2) NOT NULL CONSTRAINT DF_EmployeeResource_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc         DATETIME2(2) NOT NULL CONSTRAINT DF_EmployeeResource_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        RowVer               ROWVERSION NOT NULL,
        CONSTRAINT FK_EmployeeResource_Category FOREIGN KEY (CategoryId) REFERENCES dbo.EmployeeResourceCategory(CategoryId),
        CONSTRAINT FK_EmployeeResource_OwnerDepartment FOREIGN KEY (OwnerDepartmentId) REFERENCES dbo.Department(DeptId),
        CONSTRAINT FK_EmployeeResource_Author FOREIGN KEY (AuthorUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT FK_EmployeeResource_Approver FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT CK_EmployeeResource_Status CHECK (Status IN ('Draft', 'Published', 'Rejected', 'Archived')),
        CONSTRAINT CK_EmployeeResource_Type CHECK (ResourceType IN ('Policy', 'Form', 'Forms', 'Guide', 'FAQ', 'Checklist', 'Directory', 'Handbook', 'Reference', 'Template')),
        CONSTRAINT CK_EmployeeResource_PublicationWindow CHECK (PublishEndUtc IS NULL OR PublishStartUtc IS NULL OR PublishStartUtc < PublishEndUtc)
    );

    CREATE INDEX IX_EmployeeResource_Public
        ON dbo.EmployeeResource(Status, PublishStartUtc, PublishEndUtc)
        INCLUDE (ResourceType, CategoryId, OwnerDepartmentId, Title, Slug, UpdatedAtUtc);
    CREATE INDEX IX_EmployeeResource_Updated
        ON dbo.EmployeeResource(UpdatedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.EmployeeResourceHighlight', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeResourceHighlight
    (
        HighlightId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeResourceHighlight PRIMARY KEY,
        ResourceId    INT NOT NULL,
        HighlightText NVARCHAR(200) NOT NULL,
        SortOrder     INT NOT NULL CONSTRAINT DF_EmployeeResourceHighlight_SortOrder DEFAULT (0),
        CONSTRAINT FK_EmployeeResourceHighlight_Resource FOREIGN KEY (ResourceId) REFERENCES dbo.EmployeeResource(ResourceId) ON DELETE CASCADE
    );
    CREATE INDEX IX_EmployeeResourceHighlight_Resource ON dbo.EmployeeResourceHighlight(ResourceId, SortOrder, HighlightId);
END;

IF OBJECT_ID(N'dbo.EmployeeResourceAttachment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeResourceAttachment
    (
        AttachmentId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeResourceAttachment PRIMARY KEY,
        ResourceId     INT NOT NULL,
        FileName       NVARCHAR(255) NOT NULL,
        StoredPath     NVARCHAR(1000) NOT NULL,
        FileExtension  VARCHAR(20) NOT NULL,
        FileSizeBytes  BIGINT NOT NULL,
        MimeType       VARCHAR(150) NOT NULL,
        Description    NVARCHAR(500) NULL,
        SortOrder      INT NOT NULL CONSTRAINT DF_EmployeeResourceAttachment_SortOrder DEFAULT (0),
        UploadedByUserId INT NOT NULL,
        UploadedAtUtc  DATETIME2(2) NOT NULL CONSTRAINT DF_EmployeeResourceAttachment_UploadedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_EmployeeResourceAttachment_Resource FOREIGN KEY (ResourceId) REFERENCES dbo.EmployeeResource(ResourceId) ON DELETE CASCADE,
        CONSTRAINT FK_EmployeeResourceAttachment_User FOREIGN KEY (UploadedByUserId) REFERENCES dbo.[User](UserId)
    );
    CREATE INDEX IX_EmployeeResourceAttachment_Resource ON dbo.EmployeeResourceAttachment(ResourceId, SortOrder, AttachmentId);
END;

IF OBJECT_ID(N'dbo.EmployeeResourceRevision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeResourceRevision
    (
        RevisionId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeResourceRevision PRIMARY KEY,
        ResourceId     INT NOT NULL,
        Action         VARCHAR(50) NOT NULL,
        Remarks        NVARCHAR(500) NULL,
        SnapshotJson   NVARCHAR(MAX) NULL,
        ChangedByUserId INT NOT NULL,
        ChangedAtUtc   DATETIME2(2) NOT NULL CONSTRAINT DF_EmployeeResourceRevision_ChangedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_EmployeeResourceRevision_Resource FOREIGN KEY (ResourceId) REFERENCES dbo.EmployeeResource(ResourceId) ON DELETE CASCADE,
        CONSTRAINT FK_EmployeeResourceRevision_User FOREIGN KEY (ChangedByUserId) REFERENCES dbo.[User](UserId)
    );
    CREATE INDEX IX_EmployeeResourceRevision_Resource ON dbo.EmployeeResourceRevision(ResourceId, ChangedAtUtc DESC, RevisionId DESC);
END;

-- Retire the original mixed-department seed if this migration is applied to an existing database.
-- Existing categories and resources are retained for audit/history; inactive categories are
-- excluded from public reads and cannot be selected for new Employee Resources drafts.
UPDATE dbo.EmployeeResourceCategory
SET IsActive = 0
WHERE Slug NOT IN ('it', 'cybersecurity', 'systems-access', 'it-operations', 'it-service-management');

MERGE dbo.EmployeeResourceCategory AS target
USING (VALUES
    (N'IT Support', 'it', N'IT help, account, device, and support references.', 10),
    (N'Cybersecurity', 'cybersecurity', N'Cybersecurity guidance, incident reporting, and safe-technology practices.', 20),
    (N'Systems & Access', 'systems-access', N'Business-system access, account, and approved software references.', 30),
    (N'IT Operations', 'it-operations', N'Device setup, maintenance, service continuity, and operating procedures.', 40),
    (N'IT Service Management', 'it-service-management', N'IT service contacts, requests, notices, and escalation guidance.', 50)
) AS source(Name, Slug, Description, SortOrder)
ON target.Slug = source.Slug
WHEN MATCHED THEN UPDATE SET Name = source.Name, Description = source.Description, SortOrder = source.SortOrder, IsActive = 1
WHEN NOT MATCHED THEN INSERT(Name, Slug, Description, SortOrder) VALUES(source.Name, source.Slug, source.Description, source.SortOrder);

COMMIT TRANSACTION;

SELECT name AS TableName
FROM sys.tables
WHERE name IN ('EmployeeResourceCategory', 'EmployeeResource', 'EmployeeResourceHighlight', 'EmployeeResourceAttachment', 'EmployeeResourceRevision')
ORDER BY name;
