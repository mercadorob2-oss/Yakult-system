# Mobile "Mark As Resolved" — Full Replacement Parity with Desktop

## Objective

Port the desktop IT Call Monitoring "Mark As Resolved" dialog's full replacement logic
(both Service Only and Replacement with inventory tracking) to the YakultScanner mobile
Android app. The mobile app currently only offers a primitive radio-button choice between
"Service Only → Solved" and "Replacement (Temporary) → Resolved (Temporary)" with no
inventory impact. This spec brings it to full parity with the desktop WinForms/WPF
implementation.

## Architecture

The mobile app communicates over HTTP REST. The desktop calls repository methods directly
(ADO.NET/SQL transactions). To bridge this gap, the existing `call-ticket-action.ashx`
handler on the API side will gain a new `resolution` action that implements the same
server-side SQL transaction logic as `CallMonitoringRepository.ApplyTicketReplacementAsync()`.

### API Flow

```
Mobile (Kotlin/Retrofit)
  ↓ POST call-ticket-action.ashx  { action: "resolution", ... }
call-ticket-action.ashx (C#)
  ↓ ADO.NET SqlTransaction
  ┣━ (if unlisted old item) INSERT INTO dbo.Item
  ┣━ INSERT INTO dbo.Inventory (old unit pullout / pull-in)
  ┣━ UPDATE dbo.Item SET StockOnHand ± quantity
  ┣━ UPDATE dbo.Item SET ConditionId (old item)
  ┣━ INSERT dbo.CallTicketHistory (resolution type)
  ┣━ INSERT dbo.CallTicketNote (resolution note)
  ┣━ EXEC sp_Call_SetTicketStatus
  ┗━ Response { success: true, message: "..." }
```

### New Lookup Endpoints

The mobile needs three additional lookup endpoints to populate dialog dropdowns:

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `call-items-lookup.ashx?type=out` | Out-hardware items (old item picker) |
| GET | `call-items-lookup.ashx?type=stock` | Stock hardware items (replacement picker) |
| GET | `call-conditions.ashx` | Item conditions for old-unit assessment |

## API Changes — `call-ticket-action.ashx`

### New action: `resolution`

Accepts a POST body with the following JSON structure. The `resolutionType` field
distinguishes Service Only from Replacement.

**Service Only request:**
```json
{
  "action": "resolution",
  "ticketId": 123,
  "resolutionType": "Service Only",
  "remarks": "Replaced monitor cable.",
  "userId": 1
}
```

**Replacement request (listed old item):**
```json
{
  "action": "resolution",
  "ticketId": 123,
  "resolutionType": "Replacement",
  "remarks": "Replaced faulty keyboard.",
  "userId": 1,
  "isTemporary": false,
  "useUnlistedOldItem": false,
  "oldItemId": 456,
  "newItemId": 789,
  "quantity": 1,
  "oldItemConditionId": 2,
  "oldItemConditionRemarks": "Keys not registering",
  "oldItemRepairAction": "Repaired"
}
```

**Replacement request (unlisted old item):**
```json
{
  "action": "resolution",
  "ticketId": 123,
  "resolutionType": "Replacement",
  "remarks": "Replaced third-party dongle.",
  "userId": 1,
  "isTemporary": false,
  "useUnlistedOldItem": true,
  "oldItemId": null,
  "newItemId": 789,
  "quantity": 1,
  "oldItemConditionId": 2,
  "oldItemConditionRemarks": "Intermittent connection",
  "oldItemRepairAction": "Unrepaired",
  "unlistedOldItemName": "USB-C Dongle",
  "unlistedOldItemDescription": "Third-party USB-C to HDMI",
  "unlistedOldItemCategoryId": 5,
  "unlistedOldItemCategoryName": "Peripherals",
  "unlistedOldItemSerialNumber": "SN2024-001",
  "unlistedOldItemModelNumber": "UC-HDMI-01",
  "unlistedOldItemUnitOfMeasure": "Unit"
}
```

**Response (both types):**
```json
{
  "success": true,
  "message": "Ticket resolved as Replacement."
}
```

### Server-side implementation details

The `resolution` action handler will:

1. **Parse and validate** all input fields based on `resolutionType`
2. **If `useUnlistedOldItem == true`**: INSERT into `dbo.Item` with the provided
   unlisted item details (same fields as desktop's `itemRepo.AddItem()`)
3. **If Replacement**: run the same multi-step SQL transaction as
   `ApplyTicketReplacementAsync`:
   - Inventory IN entry for old item pullout (if `oldItemRepairAction != "Unrepaired"`)
   - Stock +quantity on old item (if returning to stock)
   - Inventory OUT entry for new item allocation
   - Concurrency-safe stock deduction (`ISNULL(StockOnHand,0) >= @Quantity`)
   - Update old item `ConditionID`, `Remarks`, `StockOnHand`
4. Insert `CallTicketHistory` with resolution type
5. Insert `CallTicketNote` with resolution summary
6. `EXEC sp_Call_SetTicketStatus @NewStatus = "Solved"` (or `"Resolved (Temporary)"`)
7. Return `{ success: true }` response

## Mobile Changes — YakultScanner

### 1. API Models (`api/CallMonitoringApiModels.kt`)

Add new data classes:

```kotlin
data class ResolutionRequest(
    val action: String = "resolution",
    val ticketId: Int,
    val resolutionType: String,       // "Service Only" | "Replacement"
    val remarks: String? = null,
    val userId: Int? = null,

    // Replacement-only fields
    val isTemporary: Boolean? = null,
    val useUnlistedOldItem: Boolean? = null,
    val oldItemId: Int? = null,
    val newItemId: Int? = null,
    val quantity: Int? = null,
    val oldItemConditionId: Int? = null,
    val oldItemConditionRemarks: String? = null,
    val oldItemRepairAction: String? = null,

    // Unlisted old item fields
    val unlistedOldItemName: String? = null,
    val unlistedOldItemDescription: String? = null,
    val unlistedOldItemCategoryId: Int? = null,
    val unlistedOldItemCategoryName: String? = null,
    val unlistedOldItemSerialNumber: String? = null,
    val unlistedOldItemModelNumber: String? = null,
    val unlistedOldItemUnitOfMeasure: String? = null
)

data class CallItemLookupDto(
    val itemId: Int,
    val displayText: String,
    val category: String?,
    val stockOnHand: Int?
)

data class CallItemLookupResponse(
    val success: Boolean,
    val items: List<CallItemLookupDto>
)

data class CallConditionDto(
    val conditionId: Int,
    val conditionName: String
)

data class CallConditionResponse(
    val success: Boolean,
    val conditions: List<CallConditionDto>
)
```

### 2. API Service (`api/ApiClient.kt`)

Add Retrofit endpoints:

```kotlin
@POST("call-ticket-action.ashx")
suspend fun callTicketResolution(@Body request: ResolutionRequest): Response<CallTicketActionResponse>

@GET("call-items-lookup.ashx")
suspend fun getCallItemsLookup(
    @Query("type") type: String        // "out" or "stock"
): Response<CallItemLookupResponse>

@GET("call-conditions.ashx")
suspend fun getCallConditions(): Response<CallConditionResponse>
```

### 3. Repository (`data/repository/CallMonitoringRepository.kt`)

Add methods:

```kotlin
suspend fun applyResolution(request: ResolutionRequest): Response<CallTicketActionResponse>
suspend fun getCallItemsLookup(type: String): Response<CallItemLookupResponse>
suspend fun getCallConditions(): Response<CallConditionResponse>
```

### 4. ViewModel (`viewmodels/CallMonitoringViewModel.kt`)

Add:
- New `MarkAsResolvedUiState` sealed class (Idle / Loading / Success / Error)
- `loadItemsForResolution(type)` — loads out/stock items and conditions
- `applyResolution(request)` — posts resolution, refreshes detail on success
- Mutable state flows for lookup data (out items, stock items, conditions)

### 5. UI — `CallMarkAsResolvedScreen.kt`

A new full-screen composable reached from `TicketDetailScreen` via navigation
`call_ticket_resolve/{ticketId}`.

**Layout (scrollable, card-based):**

| Section | Content |
|---------|---------|
| **Header** | "Mark As Resolved" title + current ticket code |
| **Step 1: Type** | Radio: "Service Only (No Parts)" / "Replacement (Parts Used)" |
| **Step 2: Old Item** (Replacement only) | Radio: "Listed in Inventory" / "Not Listed". If Listed: category filter + item dropdown. If Not Listed: Name*, Model*, Category*, Serial, Unit*, Description |
| **Step 2b: Condition** | Condition dropdown + remarks text field |
| **Step 2c: Old Unit Action** | Radio: Repaired / Damaged (Unrepaired) / Spare |
| **Step 2d: New Item** | Category filter + searchable item dropdown with stock count + quantity input |
| **Step 2e: Follow-up** | Checkbox: "Temporary replacement" |
| **Step 3: Remarks** | Multi-line remarks field |
| **Step 4: Preview** | Read-only summary: status after save, old/new items, stock impact, follow-up |
| **Confirm** | Button at bottom |

**Navigation:** The screen receives `ticketId` as nav arg. On successful resolution,
pops back and triggers a detail refresh.

### 6. New lookup API endpoints

Two new `.ashx` handlers and one existing:

- `call-items-lookup.ashx` — queries `dbo.Item` with `ItemType = 'Hardware'`:
  - `?type=out` → items with `StockOnHand = 0` (out in field)
  - `?type=stock` → items with `StockOnHand > 0` (available in stock)
  - Returns list of `itemId`, `displayText` (`Name + " (" + ModelNumber + ")"`),
    `category`, `stockOnHand`

- `call-conditions.ashx` — queries `dbo.ItemCondition` (or equivalent table):
  - Returns list of `conditionId`, `conditionName`

## Error Handling

- **Validation errors** (missing required fields, zero stock) return
  `{ success: false, message: "..." }` with HTTP 200 for easy mobile parsing
- **Server/transaction errors** return HTTP 500 with `{ success: false, message }`
- Mobile shows validation errors inline and snackbar for server errors
- Loading state disables all interactive elements to prevent double-submit

## Concurrency Safety

The `UPDATE dbo.Item SET StockOnHand = ISNULL(StockOnHand,0) - @Quantity WHERE ItemId = X AND ISNULL(StockOnHand,0) >= @Quantity`
guard (already used in the desktop implementation) prevents two operators from oversubscribing
the same stock item. This same guard is replicated in the new `resolution` action handler.
