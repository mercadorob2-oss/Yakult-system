-- Data Integrity Fix Script for SetType Values
-- This script fixes incorrect SetType values and checks data integrity

-- 1. First, let's check what invalid SetType values exist
PRINT 'Checking for invalid SetType values...'
SELECT 
    SetId,
    SetCode,
    SetType,
    DocumentNumber,
    COUNT(*) as RecordCount
FROM dbo.Sets
WHERE SetType NOT IN ('Software/License', 'Service', 'Stock Transfer', 'Stock Addition', 'Stock Subtraction')
GROUP BY SetId, SetCode, SetType, DocumentNumber
ORDER BY SetType

-- 2. Update incorrect "Invoice" SetType to appropriate values based on context
PRINT 'Fixing incorrect SetType values...'

-- First, let's update any SetType = 'Invoice' to 'Software/License' if it has items that look like software/licenses
UPDATE s
SET s.SetType = 'Software/License'
FROM dbo.Sets s
WHERE s.SetType = 'Invoice'
  AND EXISTS (
    SELECT 1 
    FROM dbo.SetItems si 
    WHERE si.SetId = s.SetId 
      AND (si.ItemType LIKE '%Software%' 
           OR si.ItemType LIKE '%License%'
           OR si.ItemName LIKE '%Software%'
           OR si.ItemName LIKE '%License%')
  )

-- Update remaining "Invoice" SetType to 'Service' as default for invoice-like documents
UPDATE dbo.Sets
SET SetType = 'Service'
WHERE SetType = 'Invoice'
  AND DocumentNumber IS NOT NULL  -- Has a document number like an invoice

-- 3. Add check constraint to prevent future invalid values
-- First drop existing constraint if it exists
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Sets_ValidSetType')
    ALTER TABLE dbo.Sets DROP CONSTRAINT CK_Sets_ValidSetType
GO

-- Add the constraint with valid values
ALTER TABLE dbo.Sets
ADD CONSTRAINT CK_Sets_ValidSetType 
CHECK (SetType IN ('Software/License', 'Service', 'Stock Transfer', 'Stock Addition', 'Stock Subtraction'))
GO

-- 4. Verify the fix
PRINT 'Verification - Current SetType distribution:'
SELECT 
    SetType,
    COUNT(*) as RecordCount
FROM dbo.Sets
GROUP BY SetType
ORDER BY SetType

-- 5. Check if the view now returns data properly
PRINT 'Testing vw_InvoiceItems view after fixes:'
SELECT TOP 10
    SetId,
    SetCode,
    SetType,
    DocumentNumber,
    CompanyName,
    VendorName
FROM dbo.vw_InvoiceItems

PRINT 'Data integrity fix completed successfully!'