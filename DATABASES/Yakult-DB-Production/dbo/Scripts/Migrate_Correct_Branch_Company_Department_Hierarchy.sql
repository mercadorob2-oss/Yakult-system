SELECT ComId, Name
FROM dbo.Company
WHERE Name LIKE '%FACTORY%';


SELECT 
    b.BranchId,
    b.Name,
    b.BranchType,
    b.IsFactory,
    c.Name AS Company
FROM dbo.Branch b
LEFT JOIN dbo.Company c ON c.ComId = b.ComId
WHERE b.Name LIKE '%CALAMBA%'
   OR b.Name LIKE '%EL SALVADOR%';

   SELECT *
FROM dbo.Branch
WHERE ComId NOT IN (SELECT ComId FROM dbo.Company);



SELECT 
    ISNULL(BranchType, '(null)') AS BranchType,
    COUNT(*) AS Count
FROM dbo.Branch
GROUP BY BranchType;


SELECT 
    c.Name AS Company,
    b.BranchType,
    b.Name AS Branch
FROM dbo.Branch b
JOIN dbo.Company c ON c.ComId = b.ComId
ORDER BY c.Name, b.BranchType;

