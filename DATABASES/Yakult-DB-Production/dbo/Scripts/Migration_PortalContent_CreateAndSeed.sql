SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Role WHERE RoleName='ContentEditor')
    INSERT dbo.Role(RoleName,Description,IsActive,DateCreated) VALUES('ContentEditor','Creates and submits portal content drafts.',1,GETDATE());

IF OBJECT_ID('dbo.PortalContentCategory','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContentCategory(
        CategoryId INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL, Slug VARCHAR(100) NOT NULL UNIQUE,
        Description NVARCHAR(500) NULL, SortOrder INT NOT NULL DEFAULT 0, IsActive BIT NOT NULL DEFAULT 1);
END;

IF OBJECT_ID('dbo.PortalContent','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContent(
        ContentId INT IDENTITY PRIMARY KEY, ContentType VARCHAR(30) NOT NULL, Slug VARCHAR(200) NOT NULL UNIQUE,
        Title NVARCHAR(180) NOT NULL, Summary NVARCHAR(500) NULL, BodyMarkdown NVARCHAR(MAX) NULL,
        CategoryId INT NOT NULL, ThumbnailUrl NVARCHAR(1000) NULL, MediaUrl NVARCHAR(1000) NULL,
        Status VARCHAR(30) NOT NULL DEFAULT 'Draft', IsFeatured BIT NOT NULL DEFAULT 0, SortOrder INT NOT NULL DEFAULT 0,
        PublishStartUtc DATETIME2(2) NULL, PublishEndUtc DATETIME2(2) NULL, AuthorUserId INT NOT NULL,
        ApprovedByUserId INT NULL, ApprovedAtUtc DATETIME2(2) NULL,
        CreatedAtUtc DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAtUtc DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PortalContent_Category FOREIGN KEY(CategoryId) REFERENCES dbo.PortalContentCategory(CategoryId),
        CONSTRAINT FK_PortalContent_Author FOREIGN KEY(AuthorUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT FK_PortalContent_Approver FOREIGN KEY(ApprovedByUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT CK_PortalContent_Type CHECK(ContentType IN('Article','Advisory','Video','FAQ','Announcement','Guide')),
        CONSTRAINT CK_PortalContent_Status CHECK(Status IN('Draft','PendingReview','Published','Rejected','Archived')));
    CREATE INDEX IX_PortalContent_Public ON dbo.PortalContent(Status,PublishStartUtc,PublishEndUtc) INCLUDE(ContentType,CategoryId,IsFeatured,SortOrder,Title,Slug);
END;

IF OBJECT_ID('dbo.PortalContentLink','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContentLink(
        LinkId INT IDENTITY PRIMARY KEY, ContentId INT NOT NULL, Label NVARCHAR(120) NOT NULL,
        Url NVARCHAR(1000) NOT NULL, LinkType VARCHAR(30) NOT NULL DEFAULT 'Related', SortOrder INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_PortalContentLink_Content FOREIGN KEY(ContentId) REFERENCES dbo.PortalContent(ContentId) ON DELETE CASCADE);
END;

IF OBJECT_ID('dbo.PortalContentRevision','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContentRevision(
        RevisionId INT IDENTITY PRIMARY KEY, ContentId INT NOT NULL, Action VARCHAR(40) NOT NULL,
        Remarks NVARCHAR(500) NULL, SnapshotJson NVARCHAR(MAX) NULL, ChangedByUserId INT NOT NULL,
        ChangedAtUtc DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PortalContentRevision_Content FOREIGN KEY(ContentId) REFERENCES dbo.PortalContent(ContentId),
        CONSTRAINT FK_PortalContentRevision_User FOREIGN KEY(ChangedByUserId) REFERENCES dbo.[User](UserId));
END;

IF OBJECT_ID('dbo.PortalContentMetricDaily','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContentMetricDaily(
        MetricDate DATE NOT NULL, EventType VARCHAR(30) NOT NULL, TargetType VARCHAR(100) NOT NULL,
        TargetKey VARCHAR(200) NOT NULL, EventCount INT NOT NULL DEFAULT 0,
        CONSTRAINT PK_PortalContentMetricDaily PRIMARY KEY(MetricDate,EventType,TargetType,TargetKey));
END;

MERGE dbo.PortalContentCategory AS t USING (VALUES
 ('IT Services','it-services','Guides and access information for Yakult IT services.',10),
 ('Cybersecurity','cybersecurity','Security awareness, advisories, and safe-working guidance.',20),
 ('Learning Center','learning','Videos, tutorials, and employee learning resources.',30),
 ('IT Updates','it-updates','Maintenance, releases, and service availability updates.',40),
 ('Policies and FAQs','policies-faqs','Policies, frequently asked questions, and reference material.',50),
 ('Company Information','company-information','Company information, leadership messages, and organizational updates.',60),
 ('News and Advisories','news-advisories','Company news, operational notices, and employee advisories.',70),
 ('Employee Resources','employee-resources','IT policies, access forms, FAQs, and technology references.',80),
 ('Events','events','Company activities, employee programs, and important dates.',90),
 ('IT Help Center','it-help','Support guides, account assistance, and troubleshooting information.',100)
) s(Name,Slug,Description,SortOrder) ON t.Slug=s.Slug
WHEN MATCHED THEN UPDATE SET Name=s.Name,Description=s.Description,SortOrder=s.SortOrder,IsActive=1
WHEN NOT MATCHED THEN INSERT(Name,Slug,Description,SortOrder) VALUES(s.Name,s.Slug,s.Description,s.SortOrder);

DECLARE @SeedAuthorId INT = (SELECT TOP (1) UserId FROM dbo.[User] WHERE IsDeveloper=1 AND IsActive=1 ORDER BY UserId);
IF @SeedAuthorId IS NOT NULL
BEGIN
    INSERT dbo.PortalContent(ContentType,Slug,Title,Summary,BodyMarkdown,CategoryId,Status,IsFeatured,SortOrder,PublishStartUtc,AuthorUserId,ApprovedByUserId,ApprovedAtUtc)
    SELECT s.ContentType,s.Slug,s.Title,s.Summary,s.BodyMarkdown,c.CategoryId,'Published',s.IsFeatured,s.SortOrder,SYSUTCDATETIME(),@SeedAuthorId,@SeedAuthorId,SYSUTCDATETIME()
    FROM (VALUES
      ('Advisory','protect-your-yakult-account','Protect your Yakult account','Use unique passwords and report suspicious sign-in prompts to IT.','Never share your password or verification code. Confirm unexpected requests through an official IT channel, and report suspicious activity immediately.',1,10,'cybersecurity'),
      ('Article','recognize-phishing-messages','Recognize phishing messages','Learn the common warning signs of fraudulent email and chat messages.','Check the sender address, unexpected urgency, unusual links, and requests for credentials. When uncertain, do not click. Forward the message to the Information Technology Department for verification.',1,20,'cybersecurity'),
      ('Guide','request-it-assistance','How to request IT assistance','Use the correct Yakult service to receive faster support.','Open the IT Services directory, select the appropriate system, and provide a clear description of the issue. Include the affected device or system and the time the problem occurred.',1,10,'it-services'),
      ('Announcement','welcome-to-the-it-portal','Welcome to the Yakult Employee Portal','A central place for employee information, services, advisories, and learning resources.','The Yakult Employee Portal provides convenient access to company information, workplace resources, learning material, support guidance, and authorized Yakult systems.',1,10,'news-advisories'),
      ('FAQ','account-access-faq','Account and access FAQ','Answers to common questions about portal accounts and system permissions.','Accounts are linked to active employee records. Access to each Yakult system is controlled by assigned roles. Contact IT when your responsibilities require additional access.',0,10,'policies-faqs')
    ) s(ContentType,Slug,Title,Summary,BodyMarkdown,IsFeatured,SortOrder,CategorySlug)
    INNER JOIN dbo.PortalContentCategory c ON c.Slug=s.CategorySlug
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PortalContent pc WHERE pc.Slug=s.Slug);
END;

COMMIT TRANSACTION;
