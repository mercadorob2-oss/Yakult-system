-- Identifies, per invoice (dbo.Set where IsInvoice = 1), where each of its receipt
-- documents is physically stored: on a local/UNC folder path (legacy) or inside the
-- database as VARBINARY bytes (current). Covers both storage generations:
--   dbo.ReceiptSet                 - legacy single-page-per-doctype columns
--                                    (SiImagePath/SiImage, DrImagePath/DrImage, PoImagePath/PoImage)
--   dbo.ReceiptSetDocumentImage    - current multi-page-per-doctype table
--                                    (ImagePath vs ImageBytes)
-- An invoice can have more than one ReceiptSet over time (renewals), linked via
-- ReceiptSet.SetId and/or dbo.ReceiptSetLink.

;WITH ReceiptSetDocs AS (
    -- Legacy columns unpivoted into one row per document type
    SELECT
        rs.ReceiptSetId,
        rs.SetId,
        DocType = 'SI',
        ImagePath = rs.SiImagePath,
        HasDbBytes = CASE WHEN rs.SiImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs
    WHERE rs.SiImagePath IS NOT NULL OR rs.SiImage IS NOT NULL

    UNION ALL
    SELECT rs.ReceiptSetId, rs.SetId, 'DR', rs.DrImagePath,
           CASE WHEN rs.DrImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs
    WHERE rs.DrImagePath IS NOT NULL OR rs.DrImage IS NOT NULL

    UNION ALL
    SELECT rs.ReceiptSetId, rs.SetId, 'PO', rs.PoImagePath,
           CASE WHEN rs.PoImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs
    WHERE rs.PoImagePath IS NOT NULL OR rs.PoImage IS NOT NULL

    UNION ALL
    -- Current multi-page table, one row per page already
    SELECT rsdi.ReceiptSetId, rs.SetId, rsdi.DocType, rsdi.ImagePath,
           CASE WHEN rsdi.ImageBytes IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSetDocumentImage rsdi
    JOIN dbo.ReceiptSet rs ON rs.ReceiptSetId = rsdi.ReceiptSetId
),
Classified AS (
    SELECT
        d.ReceiptSetId,
        -- Fall back to ReceiptSetLink for renewed sets whose direct SetId was cleared
        InvoiceSetId = COALESCE(d.SetId, rsl.SetId),
        d.DocType,
        d.ImagePath,
        d.HasDbBytes,
        StorageLocation =
            CASE
                WHEN d.ImagePath IS NOT NULL AND d.HasDbBytes = 1 THEN 'Both (Local + DB)'
                WHEN d.ImagePath IS NOT NULL AND d.HasDbBytes = 0 THEN 'Local Folder'
                WHEN d.ImagePath IS NULL     AND d.HasDbBytes = 1 THEN 'Database'
                ELSE 'Missing'
            END
    FROM ReceiptSetDocs d
    LEFT JOIN dbo.ReceiptSetLink rsl ON rsl.ReceiptSetId = d.ReceiptSetId AND d.SetId IS NULL
)
SELECT
    s.SetId,
    s.SetCode,
    s.DocumentNumber,
    c.ReceiptSetId,
    c.DocType,
    c.StorageLocation,
    c.ImagePath
FROM Classified c
JOIN dbo.[Set] s ON s.SetId = c.InvoiceSetId
WHERE s.IsInvoice = 1
ORDER BY s.SetId, c.ReceiptSetId, c.DocType;

-- Summary: count of documents per storage location, per invoice
;WITH ReceiptSetDocs AS (
    SELECT rs.ReceiptSetId, rs.SetId, DocType = 'SI', ImagePath = rs.SiImagePath,
           HasDbBytes = CASE WHEN rs.SiImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs WHERE rs.SiImagePath IS NOT NULL OR rs.SiImage IS NOT NULL
    UNION ALL
    SELECT rs.ReceiptSetId, rs.SetId, 'DR', rs.DrImagePath,
           CASE WHEN rs.DrImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs WHERE rs.DrImagePath IS NOT NULL OR rs.DrImage IS NOT NULL
    UNION ALL
    SELECT rs.ReceiptSetId, rs.SetId, 'PO', rs.PoImagePath,
           CASE WHEN rs.PoImage IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSet rs WHERE rs.PoImagePath IS NOT NULL OR rs.PoImage IS NOT NULL
    UNION ALL
    SELECT rsdi.ReceiptSetId, rs.SetId, rsdi.DocType, rsdi.ImagePath,
           CASE WHEN rsdi.ImageBytes IS NOT NULL THEN 1 ELSE 0 END
    FROM dbo.ReceiptSetDocumentImage rsdi
    JOIN dbo.ReceiptSet rs ON rs.ReceiptSetId = rsdi.ReceiptSetId
),
Classified AS (
    SELECT
        d.ReceiptSetId,
        InvoiceSetId = COALESCE(d.SetId, rsl.SetId),
        StorageLocation =
            CASE
                WHEN d.ImagePath IS NOT NULL AND d.HasDbBytes = 1 THEN 'Both (Local + DB)'
                WHEN d.ImagePath IS NOT NULL AND d.HasDbBytes = 0 THEN 'Local Folder'
                WHEN d.ImagePath IS NULL     AND d.HasDbBytes = 1 THEN 'Database'
                ELSE 'Missing'
            END
    FROM ReceiptSetDocs d
    LEFT JOIN dbo.ReceiptSetLink rsl ON rsl.ReceiptSetId = d.ReceiptSetId AND d.SetId IS NULL
)
SELECT
    s.SetId,
    s.SetCode,
    s.DocumentNumber,
    LocalFolderCount = SUM(CASE WHEN c.StorageLocation = 'Local Folder' THEN 1 ELSE 0 END),
    DatabaseCount    = SUM(CASE WHEN c.StorageLocation = 'Database' THEN 1 ELSE 0 END),
    BothCount        = SUM(CASE WHEN c.StorageLocation = 'Both (Local + DB)' THEN 1 ELSE 0 END),
    MissingCount     = SUM(CASE WHEN c.StorageLocation = 'Missing' THEN 1 ELSE 0 END)
FROM Classified c
JOIN dbo.[Set] s ON s.SetId = c.InvoiceSetId
WHERE s.IsInvoice = 1
GROUP BY s.SetId, s.SetCode, s.DocumentNumber
ORDER BY s.SetId;
