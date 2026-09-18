# RequestPortal Complete Fix - Dropdown Logic & Quantity Validation

## Issue Summary
The RequestPortal had **TWO critical problems**:
1. **Dropdown Logic**: Cartridge models with available stock were not appearing in the dropdown
2. **Quantity Validation**: Users could request more cartridges than available stock

---

## ✅ PROBLEM 1 — DROPDOWN LOGIC (FIXED)

### Root Causes Identified

#### **1. EffectiveStock calculation included portal requests (CRITICAL BUG)**
**Location**: `RequesterPortalService.cs` - Three methods

**Problem**: The EffectiveStock calculation was subtracting quantities from ALL active requests, including portal requests with `EntryType = 'None'`.

Portal requests are **INTENT-ONLY** and explicitly do NOT allocate inventory. However, the query was incorrectly treating them as inventory allocations, causing false "0 stock" results.

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
      AND ISNULL(r.EntryType, 'Negative') != 'None'  -- Exclude portal requests!
), 0)
```

#### **2. Missing AcquisitionType filter**
**Problem**: The query did not filter by `AcquisitionType`, potentially showing items that shouldn't be requestable through the portal.

**Fix Added**:
```sql
AND ISNULL(i.AcquisitionType, '') IN ('Request', 'Both', '')
```

#### **3. Missing IsTrackedAsset filter**
**Problem**: The query did not exclude fixed assets, which should not be requestable through the portal.

**Fix Added**:
```sql
AND ISNULL(i.IsTrackedAsset, 0) = 0
```

### Methods Fixed in `RequesterPortalService.cs`

1. **`GetCartridgeModelsWithAvailability()`** (lines 61-89)
   - Populates the dropdown with aggregated stock per model
   - ✅ Fixed EffectiveStock calculation
   - ✅ Added AcquisitionType filter
   - ✅ Added IsTrackedAsset filter

2. **`GetCartridgeItems()`** (lines 132-165)
   - Alternative method for retrieving cartridge items
   - ✅ Same fixes applied

3. **`ResolveCartridgeItemIdFromModelKey()`** (lines 617-642)
   - Resolves model selection to specific ItemIds
   - ✅ Same fixes applied

### Expected Dropdown Behavior After Fix

The RequestPortal dropdown now correctly lists cartridge models that meet ALL criteria:
- ✅ `StockOnHand > 0` (after subtracting non-portal requests)
- ✅ `Active = 1`
- ✅ `AcquisitionType IN ('Request', 'Both', '')` (empty string included for NULL handling)
- ✅ `IsTrackedAsset = 0` (not a fixed asset)
- ✅ Not archived
- ✅ **Portal requests (EntryType = 'None') do NOT reduce available stock**

---

## ✅ PROBLEM 2 — QUANTITY VALIDATION (FIXED)

### Issue Description
Users could request more cartridges than available stock. The quantity input had no max limit, and there was no validation on either client or server side.

### Solution Implemented

#### **Client-Side Validation (Views/Request/Index.cshtml)**

**Changes Made**:

1. **Added `data-available` attribute to dropdown options** (Line ~169):
```html
<option value="@m.ModelKey"
        data-model="@m.ModelNumber"
        data-available="@m.AvailableQuantity"
        disabled="@(!m.IsAvailable)">
    @m.DisplayName
</option>
```

2. **Enhanced `wireRowEvents()` JavaScript function** (Lines ~462-520):
```javascript
// When a model is selected from dropdown:
refSelect.addEventListener('change', function () {
    const selectedOption = refSelect.options[refSelect.selectedIndex];
    const availableQty = parseInt(selectedOption.getAttribute('data-available') || '0');

    if (qtyInput && availableQty > 0) {
        // Set max attribute
        qtyInput.max = availableQty;
        qtyInput.setAttribute('data-max-stock', availableQty);

        // Auto-correct if current value exceeds available
        const currentQty = parseInt(qtyInput.value) || 1;
        if (currentQty > availableQty) {
            qtyInput.value = availableQty;
        }

        // Add visual helper text "Max: X"
        let helperSpan = qtyInput.parentElement.querySelector('.qty-helper');
        if (!helperSpan) {
            helperSpan = document.createElement('small');
            helperSpan.className = 'form-text text-muted qty-helper';
            qtyInput.parentElement.appendChild(helperSpan);
        }
        helperSpan.textContent = `Max: ${availableQty}`;
    }
});

// Validate on input/change
qtyInput.addEventListener('change', validateQuantity);
qtyInput.addEventListener('input', validateQuantity);
```

**Behavior**:
- ✅ When user selects a cartridge model, the quantity input's `max` attribute is set to available stock
- ✅ If user tries to enter a quantity > available, it's auto-corrected to the max
- ✅ Up/down arrows respect the max limit
- ✅ Visual helper text shows "Max: X" below the quantity input
- ✅ For out-of-stock models (portal requests are intent-only), no max limit is enforced

#### **Server-Side Validation (Controllers/RequestController.cs)**

**Changes Made** (Lines ~140-165):
```csharp
// SERVER-SIDE VALIDATION: Verify requested quantities don't exceed available stock
var availableModels = _portalService.GetCartridgeModelsWithAvailability();
var availableStockDict = availableModels.ToDictionary(m => m.ModelKey, m => m.AvailableQuantity);

if (model.RequestItems != null && model.RequestItems.Any())
{
    foreach (var item in model.RequestItems.Where(x => !string.IsNullOrWhiteSpace(x.ModelKey)))
    {
        if (availableStockDict.TryGetValue(item.ModelKey, out int availableStock))
        {
            if (item.Quantity > availableStock)
            {
                ModelState.AddModelError("",
                    $"Quantity for model '{item.CartridgeModel}' ({item.Quantity}) exceeds available stock ({availableStock}). " +
                    $"Please reduce the quantity to {availableStock} or less.");

                // Reload dropdown and return to form
                model.CartridgeModels = availableModels;
                // ... reload other dropdowns
                return View("Index", model);
            }
        }
    }
}
```

**Behavior**:
- ✅ Before processing the request, validates each item's quantity against available stock
- ✅ If any quantity exceeds available stock, displays error message and returns to form
- ✅ Error message shows: "Quantity for model 'HP 83A' (5) exceeds available stock (2). Please reduce the quantity to 2 or less."
- ✅ Prevents inventory over-allocation attacks via direct POST manipulation

---

## Files Modified

### Backend (Problem 1 - Dropdown Logic)
✅ `Request Portal - NET\Inventory.RequestPortal\Inventory.RequestPortal\Services\RequesterPortalService.cs`
- Lines 61-89: `GetCartridgeModelsWithAvailability()`
- Lines 132-165: `GetCartridgeItems()`
- Lines 617-642: `ResolveCartridgeItemIdFromModelKey()`

### Frontend (Problem 2 - Quantity Validation)
✅ `Request Portal - NET\Inventory.RequestPortal\Inventory.RequestPortal\Views\Request\Index.cshtml`
- Line ~169: Added `data-available` attribute to dropdown options
- Lines ~462-520: Enhanced `wireRowEvents()` with quantity validation

### Controller (Problem 2 - Server-Side Validation)
✅ `Request Portal - NET\Inventory.RequestPortal\Inventory.RequestPortal\Controllers\RequestController.cs`
- Lines ~140-165: Added server-side quantity validation in `Create()` action

---

## Verification Steps

### Problem 1 - Dropdown Logic
1. **Test with existing portal requests**:
   - ✅ Confirm that cartridges with active portal requests now appear in the dropdown
   - ✅ Verify available stock is NOT reduced by portal request quantities

2. **Test with non-portal requests**:
   - ✅ Verify that cartridges with active non-portal requests (EntryType = 'Negative') still have their stock correctly reduced

3. **Test AcquisitionType filtering**:
   - ✅ Confirm that only items with AcquisitionType = 'Request' or 'Both' appear
   - ❌ Items with AcquisitionType = 'Purchase' should be excluded

4. **Test fixed asset exclusion**:
   - ✅ Confirm that items with IsTrackedAsset = 1 do NOT appear in the dropdown

### Problem 2 - Quantity Validation
1. **Test client-side max enforcement**:
   - ✅ Select a cartridge model with available stock (e.g., "Available: 2")
   - ✅ Try to enter quantity > 2 using keyboard → auto-corrected to 2
   - ✅ Try to use up arrow to exceed max → stops at 2
   - ✅ Visual helper text shows "Max: 2"

2. **Test server-side validation**:
   - ✅ Use browser dev tools to bypass client-side validation
   - ✅ POST a request with quantity > available stock
   - ✅ Server rejects with error message and returns to form

3. **Test multi-row behavior**:
   - ✅ Add multiple cartridge models to the request
   - ✅ Each row enforces its own max based on selected model
   - ✅ Changing model in dropdown updates max dynamically

4. **Test out-of-stock models**:
   - ✅ For models with 0 stock, no max limit is enforced (portal requests are intent-only)

---

## Architecture Alignment

These fixes align the RequestPortal with the system's architectural principles:

1. **Portal requests are INTENT-ONLY** and do NOT affect inventory until IT fulfillment
2. **Only requestable items appear** in the dropdown (AcquisitionType filtering)
3. **Fixed assets are excluded** from self-service requests
4. **Users cannot over-request** inventory that doesn't exist (quantity validation)
5. **Defense in depth** with both client-side (UX) and server-side (security) validation

---

## Testing Notes

**Before Fix**:
- ❌ Dropdown showed 0 cartridges even with StockOnHand = 2
- ❌ Users could request 999 cartridges when only 2 available

**After Fix**:
- ✅ Dropdown shows cartridges with StockOnHand = 2
- ✅ Quantity input max = 2 when cartridge is selected
- ✅ Server validates and rejects over-requests
- ✅ Portal requests don't reduce dropdown availability

---

## No Changes Made To
⚠️ `ViewCartridgesPage.cs` - **NOT modified** (as required)
