# SubmissionSessionId Implementation

## Overview
Added `SubmissionSessionId` column to `dbo.Request` table to track multi-model cartridge submissions from the same submit action in the Request Portal.

## Problem Statement
The Request Portal creates one `dbo.Request` row per cartridge model when users submit multi-model requests. Previously, there was no reliable way to identify which request rows came from the same submission event, causing grouping issues downstream in the Cartridge Management Portal.

## Solution
Introduced a `SubmissionSessionId` (UNIQUEIDENTIFIER) column that:
- Is generated once per submit action (one GUID per form submission)
- Is assigned to ALL `dbo.Request` rows created by that submission
- Remains NULL for legacy requests (backward compatible)
- Does NOT replace `ReqId` (which remains the unique identifier per model/request row)

## Changes Made

### 1. Database Migration
**File:** `c:\Users\shawn\YIMS\Yakult.Inventory.App\Database_Migration_Add_SubmissionSessionId_To_Request.sql`

- Adds nullable `SubmissionSessionId UNIQUEIDENTIFIER` column to `dbo.Request`
- Creates filtered non-clustered index `IX_Request_SubmissionSessionId` for query performance
- Backward compatible: existing requests will have NULL values

**To apply migration:**
```sql
-- Run this script against the YakultInventory database
-- Script is idempotent and safe to run multiple times
```

### 2. Data Model Updates
**File:** `Inventory.RequestPortal\Models\RequestDto.cs`

Added property:
```csharp
public Guid? SubmissionSessionId { get; set; }
```

### 3. Repository Layer
**File:** `Inventory.RequestPortal\Repositories\RequestRepository.cs`

Updated `AddRequest` method:
- Modified INSERT statement to include `SubmissionSessionId` column
- Added parameter binding: `@SubmissionSessionId` (nullable)
- Handles NULL values correctly for backward compatibility

### 4. Service Layer
**File:** `Inventory.RequestPortal\Services\RequesterPortalService.cs`

Updated both submission methods:

#### `CreateCartridgeRequestByModel` (primary multi-model method)
- Generates `Guid.NewGuid()` once at the start of the method
- Assigns the same GUID to all `RequestDto` objects created in the loop
- Single-model submissions also get a SubmissionSessionId

#### `CreateCartridgeRequest` (legacy single-model method)
- Generates `Guid.NewGuid()` for consistency
- Assigns to the single `RequestDto` object

### 5. Controller Layer
**File:** `Inventory.RequestPortal\Controllers\RequestController.cs`

No changes required. The controller calls the service method, which now handles SubmissionSessionId generation internally.

## Behavior

### Multi-Model Submission Example
User submits a request for 3 cartridge models:
- Model A, Qty 5
- Model B, Qty 10
- Model C, Qty 3

**Result in dbo.Request:**
| ReqId | ItemId | Quantity | SubmissionSessionId |
|-------|--------|----------|---------------------|
| 1001  | 45     | 5        | `abc123...` (same GUID) |
| 1002  | 67     | 10       | `abc123...` (same GUID) |
| 1003  | 89     | 3        | `abc123...` (same GUID) |

### Single-Model Submission Example
User submits a request for 1 cartridge model:
- Model A, Qty 5

**Result in dbo.Request:**
| ReqId | ItemId | Quantity | SubmissionSessionId |
|-------|--------|----------|---------------------|
| 1004  | 45     | 5        | `def456...` (unique GUID) |

### Legacy Requests
Existing requests created before this change:
| ReqId | ItemId | Quantity | SubmissionSessionId |
|-------|--------|----------|---------------------|
| 900   | 45     | 5        | NULL |
| 901   | 67     | 10       | NULL |

## Backward Compatibility

✅ **Fully backward compatible:**
- Column is nullable
- Legacy requests have NULL `SubmissionSessionId`
- No breaking changes to existing queries
- Yakult.Inventory.App (WinForms) does not need immediate updates
- Cartridge Management Portal can query by `SubmissionSessionId` when available, fall back to other logic for NULL values

## Testing Recommendations

1. **Run Database Migration:**
   - Execute `Database_Migration_Add_SubmissionSessionId_To_Request.sql`
   - Verify column exists: `SELECT TOP 1 * FROM dbo.Request`

2. **Test Single-Model Submission:**
   - Submit a request with 1 cartridge model
   - Verify `SubmissionSessionId` is populated with a GUID
   - Verify request is created successfully

3. **Test Multi-Model Submission:**
   - Submit a request with 3+ cartridge models
   - Query: `SELECT ReqId, ItemId, SubmissionSessionId FROM dbo.Request WHERE SubmissionSessionId = '<guid>'`
   - Verify all rows share the same `SubmissionSessionId`

4. **Test Backward Compatibility:**
   - Query existing requests: `SELECT COUNT(*) FROM dbo.Request WHERE SubmissionSessionId IS NULL`
   - Verify legacy requests still work in Cartridge Management Portal

5. **Test Grouping Query (for Cartridge Management Portal):**
```sql
-- Group requests by submission session
SELECT 
    SubmissionSessionId,
    COUNT(*) AS ModelCount,
    SUM(Quantity) AS TotalQuantity,
    MIN(DateCreated) AS SubmissionDate
FROM dbo.Request
WHERE SubmissionSessionId IS NOT NULL
GROUP BY SubmissionSessionId
ORDER BY MIN(DateCreated) DESC
```

## Future Enhancements

The Cartridge Management Portal (Yakult.Inventory.App) can now:
- Group multi-model requests by `SubmissionSessionId`
- Display submission-level summaries
- Process all models from a submission together
- Track submission status as a unit

## Notes

- **ReqId remains the primary key** and unique identifier per request row
- **SubmissionSessionId is a grouping identifier** for related requests
- One submission = One GUID = Multiple ReqIds (one per model)
- The GUID is generated in the service layer, not the database (no default constraint)
- This implementation does NOT modify Yakult.Inventory.App code
- This implementation does NOT add file/API communication
- This implementation does NOT alter the one-request-row-per-model behavior
