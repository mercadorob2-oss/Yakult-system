# RequestPortal Cartridge Dropdown Bug Fix

## Issue Summary
The RequestPortal cartridge model dropdown was showing **no available cartridges** even though ViewCartridgesPage correctly displayed stock (StockOnHand = 2).

---

## Root Causes Identified

### **1. EffectiveStock calculation included portal requests (CRITICAL BUG)**
**Location**: `RequesterPortalService.cs` - lines 70-76, 149-155, 626-632

**Problem**: The EffectiveStock calculation was subtracting quantities from ALL active requests, including portal requests with `EntryType = 'None'`.

Portal requests are **INTENT-ONLY** and explicitly do NOT allocate inventory (as documented throughout the codebase). However, the query was incorrectly treating them as inventory allocations, causing false "0 stock" results.

**Before** (INCORRECT):
```sql
i.StockOnHand - ISNULL((
    SELECT SUM(r.Quantity)
    FROM dbo.Request r
    WHERE r.ItemId = i.ItemId
      AND r.Active = 1
), 0)
```

**After** (CORRECT):
```sql
i.StockOnHand - ISNULL((
    SELECT SUM(r.Quantity)
    FROM dbo.Request r
    WHERE r.ItemId = i.ItemId
      AND r.Active = 1
      AND ISNULL(r.EntryType, 'Negative') != 'None'  -- Exclude portal requests
), 0)
```

---

### **2. Missing AcquisitionType filter**
**Location**: `RequesterPortalService.cs` - WHERE clauses in all three methods

**Problem**: The query did not filter by `AcquisitionType`, potentially showing items that shouldn't be requestable through the portal.

**Fix Added**:
```sql
AND ISNULL(i.AcquisitionType, '') IN ('Request', 'Both', '')
```

This ensures only items acquired through requests (or both purchase and request) appear in the dropdown.

---

### **3. Missing IsTrackedAsset filter**
**Location**: `RequesterPortalService.cs` - WHERE clauses in all three methods

**Problem**: The query did not exclude fixed assets, which should not be requestable through the portal.

**Fix Added**:
```sql
AND ISNULL(i.IsTrackedAsset, 0) = 0
```

This excludes items tracked as fixed assets from the dropdown.

---

## Methods Fixed

All three methods in `RequesterPortalService.cs` were updated with the same fixes:

1. **`GetCartridgeModelsWithAvailability()`** (lines 53-116)
   - Used by the dropdown to show aggregated stock per model

2. **`GetCartridgeItems()`** (lines 124-194)
   - Alternative method for retrieving cartridge items

3. **`ResolveCartridgeItemIdFromModelKey()`** (lines 610-636)
   - Resolves model selection to specific ItemIds

---

## Expected Behavior After Fix

The RequestPortal dropdown will now correctly list cartridge models that meet ALL criteria:
- ✅ `StockOnHand > 0` (after subtracting non-portal requests)
- ✅ `Active = 1`
- ✅ `AcquisitionType IN ('Request', 'Both', '')` (empty string included for NULL handling)
- ✅ `IsTrackedAsset = 0` (not a fixed asset)
- ✅ Not archived
- ✅ Portal requests (EntryType = 'None') do NOT reduce available stock

---

## Verification Steps

1. **Test with existing portal requests**:
   - Confirm that cartridges with active portal requests now appear in the dropdown
   - Verify available stock is NOT reduced by portal request quantities

2. **Test with non-portal requests**:
   - Verify that cartridges with active non-portal requests (EntryType = 'Negative') still have their stock correctly reduced

3. **Test AcquisitionType filtering**:
   - Confirm that only items with AcquisitionType = 'Request' or 'Both' appear
   - Items with AcquisitionType = 'Purchase' should be excluded

4. **Test fixed asset exclusion**:
   - Confirm that items with IsTrackedAsset = 1 do NOT appear in the dropdown

---

## Files Modified

- ✅ `Request Portal - NET\Inventory.RequestPortal\Inventory.RequestPortal\Services\RequesterPortalService.cs`
- ⚠️ ViewCartridgesPage.cs was NOT modified (as required)

---

## Architecture Notes

This fix aligns the RequestPortal dropdown logic with the system's architectural principle that:

> **Portal requests are INTENT-ONLY and do NOT affect inventory until IT fulfillment.**

The dropdown now correctly reflects this by excluding portal requests from stock availability calculations while still deducting actual inventory-impacting requests.
