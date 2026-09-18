# Item Audit Trail Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `dbo.ItemAuditTrail` writes to every item-stock/status mutation in the desktop app (`Yakult.Inventory.App`) that is currently unaudited, so the Item Movement Audit pages show the full picture.

**Architecture:** Reuse the two existing audit-write channels — `ItemAuditTrailWriter.TryLog`/`TryLogWithResult` (in-transaction, best-effort) and `ItemAuditTrailRepository.LogActionAsync` (post-commit, best-effort). The single exception is the Sell/Dispose flow, which gets an in-transaction, fail-loud insert inside its existing SQL batch (audit failure rolls back the whole decision). All changes are edits to existing files — **no new `.cs` files, therefore no csproj changes** (avoids the csproj `Compile Include` rule entirely).

**Tech Stack:** C# / WinForms + WPF (.NET Framework 4.8), ADO.NET `SqlClient`, T-SQL stored procedures. Build: `MSBuild.exe` (VS 18 Community).

**Scope decisions (from user):**
- Desktop app only — the web portal (Request Portal) is NOT in scope.
- Cartridge lifecycle is NOT touched.
- Coverage only — no schema hardening (no FKs, no `Notes` type change, no action-code enum).
- Sell/Dispose audit = in-transaction, **fail loudly**. All other new writes follow the existing best-effort patterns.

**Verification approach:** This repo has no test project (confirmed — no test csproj exists). Verification per task = MSBuild Debug build of `Yakult.Inventory.App.csproj` (must be 0 errors) plus a manual SQL verification query. Final task runs a full build and a detection script.

---

## File Structure

| File | Change |
|---|---|
| `Yakult.Inventory.App\Repositories\ItemLifecycleDecisionRepository.cs` | Add audit INSERT to SQL batch (fail-loud) |
| `Yakult.Inventory.App\Repositories\InvoiceRepository.cs` | Audit in `DeleteInvoiceSetItemAsync` + `DeleteInvoiceAndRestoreStock` |
| `Yakult.Inventory.App\Wpf\Items\ViewModels\ItemsPageViewModel.Queries.cs` | Audit in `ArchiveItemAsync` |
| `DATABASES\Yakult-DB-Production\dbo\Stored Procedures\sp_ArchiveItem.sql` | Audit INSERT inside sproc |
| `Yakult.Inventory.App\Pages\Archive\ItemArchiveDetailsDialog.cs` | Audit in item restore |
| `Yakult.Inventory.App\Pages\Archive\ArchiveDetailsDialog.cs` | Audit in `case "Item"` restore |
| `Yakult.Inventory.App\Wpf\Archive\ViewModels\ArchiveDetailsViewModel.cs` | Audit in `case "Item"` restore |
| `DATABASES\Yakult-DB-Production\dbo\Stored Procedures\usp_Request_Delete.sql` | Audit INSERT inside sproc |
| `Yakult.Inventory.App\Repositories\SetRepository.cs` | Audit in `ApplySetItemUpgradesAsync` |
| `Yakult.Inventory.App\Repositories\InventoryRepository.cs` | Audit in `CreateInventoryAsync` (post-commit) |
| `Yakult.Inventory.App\Pages\Inventory\EditInventoryDialog.cs` | Audit in inline tx save handler |
| `Yakult.Inventory.App\Pages\Admin\Reference-Data\MasterDataUpdatePage.cs` | Audit per item in bulk-save loop |
| `Yakult.Inventory.App\Repositories\VendorRepository.cs` | Audit in `UpdateItemVendorAsync` |
| `Yakult.Inventory.App\Repositories\RenewalRepository.cs` | Audit in `UpdateItemDates` + `ArchiveSetItem` |
| `Yakult.Inventory.App\Repositories\InvoiceLicenseReviewRepository.cs` | Audit per decision in `SaveReviewDecisionsAsync` |
| `Yakult.Inventory.App\Repositories\ItemRepository.cs` | Audit per item in `UpdateCellPhoneDetails` |
| `Yakult.Inventory.App\Repositories\RequestRepository.cs` | Audit in `FulfillRequest` |

Action vocabulary used (matches existing free-form style): `Item Sold`, `Item Disposed`, `Item Restored`, `Item Archived`, `Item Archived - Deactivated`, `Invoice Line Deleted - Stock Restored`, `Invoice Deleted - Stock Restored`, `Request Deleted - Stock Restored`, `Item Upgraded - Old Item Returned`, `Item Upgraded - New Item Allocated`, `Inventory Entry Added`, `Inventory Entry Updated`, `Item Bulk Updated`, `Item Vendor Updated`, `Item Renewal Dates Updated`, `Item Renewal Archived`, `License Review Classified`, `Cellphone Details Updated`, `Request Fulfilled`.

`ReferenceType` values stay within the existing vocabulary: `Item`, `Set`, `Request`, `Update`, `CallTicket`. `Direction`: `IN` = stock returned/added, `OUT` = stock deducted/allocated.

---

### Task 1: Sell/Dispose audit (P0 — in-transaction, fail-loud)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\ItemLifecycleDecisionRepository.cs:57-150` (SQL batch inside `ExecuteSellOrDisposeAsync`)

- [ ] **Step 1: Extend the item SELECT to also fetch SerialNumber**

In the SQL batch, change:

```sql
SELECT
    @StockOnHand = ISNULL(i.StockOnHand, 0),
    @ConditionId = i.ConditionId
FROM dbo.Item i WITH (UPDLOCK, HOLDLOCK)
WHERE i.ItemId = @ItemId;
```

to:

```sql
SELECT
    @StockOnHand = ISNULL(i.StockOnHand, 0),
    @ConditionId = i.ConditionId,
    @SerialNumber = i.SerialNumber
FROM dbo.Item i WITH (UPDLOCK, HOLDLOCK)
WHERE i.ItemId = @ItemId;
```

and add the declaration next to the existing `DECLARE @StockOnHand INT = 0;` / `DECLARE @ConditionId INT = NULL;`:

```sql
DECLARE @SerialNumber NVARCHAR(100) = NULL;
```

- [ ] **Step 2: Insert the audit row before the final `SELECT @DecisionId;`**

In the batch, immediately after the `MERGE dbo.ArchiveStatus ...` block (and before `SELECT @DecisionId;`), add:

```sql
INSERT INTO dbo.ItemAuditTrail
    (ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
     DepartmentId, DepartmentName, BranchId, BranchName, Direction,
     Status, ReferenceType, ReferenceId, Notes, CreatedBy)
VALUES
    (@ItemId, @SerialNumber, @AuditAction, SYSDATETIME(), @DecidedByUserId, @DecidedByDisplayName,
     NULL, NULL, NULL, NULL, 'OUT', 'Executed', 'Item', @ItemId, @AuditNotes, @CreatedBy);
```

This runs inside the same transaction — if it fails, the error bubbles up and the C# `catch` rolls back the entire decision (fail-loud).

- [ ] **Step 3: Add the new C# parameters**

In `ItemLifecycleDecisionRepository.cs` next to the existing `cmd.Parameters.AddWithValue(...)` calls (after `@ArchiveReason`), add:

```csharp
cmd.Parameters.AddWithValue("@SerialNumber", (object)null);
cmd.Parameters.AddWithValue("@AuditAction", decisionTypeName == "SELL" ? "Item Sold" : "Item Disposed");
cmd.Parameters.AddWithValue("@AuditNotes", inventoryDescription);
cmd.Parameters.AddWithValue("@CreatedBy", AppSession.CurrentUserName ?? "System");
```

Notes:
- `@SerialNumber` is a placeholder param consumed only by the batch's `@SerialNumber = i.SerialNumber` assignment; the batch value is the fetched one.
- `@DecidedByDisplayName` is **not** a param yet — the C# method holds `decidedByDisplayName`. Add it as a parameter:

```csharp
cmd.Parameters.AddWithValue("@DecidedByDisplayName", (object)decidedByDisplayName ?? DBNull.Value);
```

- Ensure `using Yakult.Inventory.App.Session;` is present (the file currently has `using Yakult.Inventory.App.Core;`; add the Session using at the top).

- [ ] **Step 4: Build**

Run:
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Yakult.Inventory.App\Yakult.Inventory.App.csproj" /t:Build /p:Configuration=Debug /v:minimal /nologo
```
Expected: build succeeds (0 errors, 0 warnings introduced by this change).

- [ ] **Step 5: Commit**

```bash
git add Yakult.Inventory.App/Repositories/ItemLifecycleDecisionRepository.cs
git commit -m "audit: trace Sell/Dispose lifecycle decisions in ItemAuditTrail (in-tx, fail-loud)"
```

---

### Task 2: Invoice line deletion audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\InvoiceRepository.cs:928-982` (inside `DeleteInvoiceSetItemAsync` tx)

- [ ] **Step 1: Add SerialNumber to the line lookup SELECT**

Change the `selectSql` const so the `dbo.Item` join also returns the serial:

```csharp
const string selectSql = @"
SELECT TOP (1)
    si.ItemId,
    si.Quantity,
    ISNULL(i.AffectsInventory, 0) AS AffectsInventory,
    i.SerialNumber
FROM dbo.SetItem si
LEFT JOIN dbo.Item i ON si.ItemId = i.ItemId
WHERE si.SetId = @SetId
  AND si.SetItemId = @SetItemId;";
```

Declare `string serialNumber = null;` next to `int itemId = 0;` (line ~924) and populate it in the reader:

```csharp
serialNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
```

- [ ] **Step 2: Write the audit row in the tx when stock is restored**

Inside the `if (affectsInventory && itemId > 0 && quantity != 0)` block, after the `deleteInventorySql` execution (after line ~981) and before the block closes, add:

```csharp
ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
{
    ItemId = itemId,
    SerialNumber = serialNumber,
    Action = "Invoice Line Deleted - Stock Restored",
    ActionTime = DateTime.Now,
    Direction = "IN",
    Status = "Completed",
    ReferenceType = "Set",
    ReferenceId = setId,
    Notes = $"Stock restored for {quantity} unit(s) after invoice line deletion.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 3: Ensure the usings exist**

Confirm the file already imports `Yakult.Inventory.App.Models` (for `ItemAuditTrailDto`) and `Yakult.Inventory.App.Session`; if not, add them.

- [ ] **Step 4: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Yakult.Inventory.App/Repositories/InvoiceRepository.cs
git commit -m "audit: trace invoice line deletions with stock restore"
```

---

### Task 3: Invoice deletion (stock restore) audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\InvoiceRepository.cs:1147-1188` (inside `DeleteInvoiceAndRestoreStock` tx)

- [ ] **Step 1: Add SerialNumber to the set-items lookup**

Change `getSetItemsSql` to:

```csharp
string getSetItemsSql = @"
    SELECT si.ItemId, si.Quantity, i.AffectsInventory, i.SerialNumber
    FROM SetItem si
    INNER JOIN Item i ON si.ItemId = i.ItemId
    WHERE si.SetId = @SetId";
```

Update the tuple type and reader:

```csharp
List<(int ItemId, int Quantity, bool AffectsInventory, string SerialNumber)> setItems = new List<(int, int, bool, string)>();
```

and in the loop:

```csharp
string serialNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
setItems.Add((itemId, quantity, affectsInventory, serialNumber));
```

- [ ] **Step 2: Write one audit row per restored item**

Inside the `foreach (var item in setItems)` restore loop, after the `restoreStockSql` execution (after line ~1186) add:

```csharp
ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
{
    ItemId = item.ItemId,
    SerialNumber = item.SerialNumber,
    Action = "Invoice Deleted - Stock Restored",
    ActionTime = DateTime.Now,
    Direction = "IN",
    Status = "Completed",
    ReferenceType = "Set",
    ReferenceId = setId,
    Notes = $"Stock restored for {item.Quantity} unit(s) after invoice deletion.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 3: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Repositories/InvoiceRepository.cs
git commit -m "audit: trace invoice deletion stock restores per item"
```

---

### Task 4: WPF grid archive/deactivate audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Wpf\Items\ViewModels\ItemsPageViewModel.Queries.cs:419-462` (`ArchiveItemAsync`)

- [ ] **Step 1: Fetch the serial before archiving**

Inside `Task.Run`, after the transaction begins (line ~428), add:

```csharp
string serialNumber = null;
using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, transaction))
{
    cmd.Parameters.AddWithValue("@ItemId", itemId);
    var result = cmd.ExecuteScalar();
    serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
}
```

- [ ] **Step 2: Write the audit row in the tx**

After the `if (deactivate) { ... }` block (after line ~450) and before `transaction.Commit();`, add:

```csharp
ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
{
    ItemId = itemId,
    SerialNumber = serialNumber,
    Action = deactivate ? "Item Archived - Deactivated" : "Item Archived",
    ActionTime = DateTime.Now,
    Status = "Completed",
    ReferenceType = "Item",
    ReferenceId = itemId,
    Notes = $"Archived{(string.IsNullOrWhiteSpace(reason) ? "" : $" • Reason: {reason}")}.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 3: Ensure usings exist**

Add `using Yakult.Inventory.App.Models;` and `using Yakult.Inventory.App.Repositories;` if not already present (check the top of the file; `AppSession` comes from `Yakult.Inventory.App.Session` — add that too if missing).

- [ ] **Step 4: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Yakult.Inventory.App/Wpf/Items/ViewModels/ItemsPageViewModel.Queries.cs
git commit -m "audit: trace WPF grid archive/deactivate of items"
```

---

### Task 5: Legacy sp_ArchiveItem sproc audit (P1)

**Files:**
- Modify: `DATABASES\Yakult-DB-Production\dbo\Stored Procedures\sp_ArchiveItem.sql`

- [ ] **Step 1: Add the audit INSERT before COMMIT**

In `sp_ArchiveItem.sql`, after the final `UPDATE dbo.Item ... WHERE ItemId = @ItemId;` (line ~64) and before `COMMIT TRANSACTION;` (line ~66), add:

```sql
        -- Audit trail entry (in-transaction)
        INSERT INTO dbo.ItemAuditTrail
            (ItemId, SerialNumber, Action, ActionTime, Direction, Status,
             ReferenceType, ReferenceId, Notes, CreatedBy)
        SELECT
            i.ItemId, i.SerialNumber, 'Item Archived', GETDATE(), NULL, 'Completed',
            'Item', i.ItemId, 'Archived via sp_ArchiveItem' + ISNULL(' • ' + @ArchiveReason, ''),
            @ArchivedBy
        FROM dbo.Item i
        WHERE i.ItemId = @ItemId;
```

- [ ] **Step 2: Verify the SQL**

Run the file against a scratch DB (via `sqlcmd` or SSMS) to confirm the `CREATE PROCEDURE` parses and the `BEGIN TRY/TRANSACTION` nesting is intact. If no SQL connection is available, visually verify the edit sits inside the `BEGIN TRY ... COMMIT TRANSACTION` block.

- [ ] **Step 3: Commit**

```bash
git add DATABASES/Yakult-DB-Production/dbo/Stored\ Procedures/sp_ArchiveItem.sql
git commit -m "audit: trace sp_ArchiveItem archives in ItemAuditTrail"
```

---

### Task 6: Item restore dialogs audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Pages\Archive\ItemArchiveDetailsDialog.cs:143-151`
- Modify: `Yakult.Inventory.App\Pages\Archive\ArchiveDetailsDialog.cs:181-185`
- Modify: `Yakult.Inventory.App\Wpf\Archive\ViewModels\ArchiveDetailsViewModel.cs:498-502`

- [ ] **Step 1: ItemArchiveDetailsDialog — audit after Active=1**

In `ItemArchiveDetailsDialog.cs`, after the `UPDATE dbo.Item SET Active = 1 ...` command (after line ~149) and before `tx.Commit();`, add:

```csharp
using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
{
    cmd.Parameters.AddWithValue("@ItemId", _itemId);
    var result = cmd.ExecuteScalar();
    ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
    {
        ItemId = _itemId,
        SerialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result),
        Action = "Item Restored",
        ActionTime = DateTime.Now,
        Direction = "IN",
        Status = "Completed",
        ReferenceType = "Item",
        ReferenceId = _itemId,
        Notes = "Item restored from archive.",
        CreatedBy = AppSession.CurrentUserName ?? "System"
    });
}
```

Add missing usings (`Yakult.Inventory.App.Models`, `Yakult.Inventory.App.Repositories`, `Yakult.Inventory.App.Session`) if absent.

- [ ] **Step 2: ArchiveDetailsDialog — audit in `case "Item"`**

In `ArchiveDetailsDialog.cs` `RestoreEntityCascade`, replace the `case "Item":` body (lines 181-185) with:

```csharp
case "Item":
    ExecuteNonQuery(con, tx,
        "UPDATE dbo.Item SET Active = 1 WHERE ItemId = @Id",
        ("@Id", entityId));
    LogItemRestored(con, tx, entityId);
    break;
```

Add this helper method to the class (e.g. right after `RestoreEntityCascade`):

```csharp
private void LogItemRestored(SqlConnection con, SqlTransaction tx, int itemId)
{
    try
    {
        string serial = null;
        using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
        {
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            var result = cmd.ExecuteScalar();
            serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
        }
        ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
        {
            ItemId = itemId,
            SerialNumber = serial,
            Action = "Item Restored",
            ActionTime = DateTime.Now,
            Direction = "IN",
            Status = "Completed",
            ReferenceType = "Item",
            ReferenceId = itemId,
            Notes = "Item restored from archive.",
            CreatedBy = AppSession.CurrentUserName ?? "System"
        });
    }
    catch { /* best-effort */ }
}
```

Add missing usings if absent.

- [ ] **Step 3: WPF ArchiveDetailsViewModel — audit in `case "Item"`**

In `ArchiveDetailsViewModel.cs` `RestoreEntityCascade`, replace the `case "Item":` body (lines 498-502) with:

```csharp
case "Item":
    ExecNonQuery(con, tx,
        "UPDATE dbo.Item SET Active = 1 WHERE ItemId = @Id",
        ("@Id", entityId));
    LogItemRestored(con, tx, entityId);
    break;
```

and add the same `LogItemRestored` helper (identical code to Step 2, using `SqlCommand` with `con, tx`, and `ItemAuditTrailWriter.TryLog`).

Add missing usings (`Yakult.Inventory.App.Models`, `Yakult.Inventory.App.Repositories`, `Yakult.Inventory.App.Session`) if absent.

- [ ] **Step 4: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Yakult.Inventory.App/Pages/Archive/ItemArchiveDetailsDialog.cs Yakult.Inventory.App/Pages/Archive/ArchiveDetailsDialog.cs Yakult.Inventory.App/Wpf/Archive/ViewModels/ArchiveDetailsViewModel.cs
git commit -m "audit: trace item restores across archive dialogs"
```

---

### Task 7: usp_Request_Delete sproc audit (P1)

**Files:**
- Modify: `DATABASES\Yakult-DB-Production\dbo\Stored Procedures\usp_Request_Delete.sql`

- [ ] **Step 1: Add the audit INSERT after the stock-restore step**

In `usp_Request_Delete.sql`, after the STEP 1 `UPDATE i ... WHERE i.AffectsInventory = 1;` block (after line ~57) and before `PRINT 'Stock restored...'`, add:

```sql
        -- STEP 1b: Audit trail entries for restored stock
        INSERT INTO dbo.ItemAuditTrail
            (ItemId, SerialNumber, Action, ActionTime, Direction, Status,
             ReferenceType, ReferenceId, Notes, CreatedBy)
        SELECT
            i.ItemId, i.SerialNumber, 'Request Deleted - Stock Restored', GETDATE(), 'IN', 'Completed',
            'Request', r.ReqId,
            'Stock restored for ' + CAST(r.Quantity AS VARCHAR(10)) + ' unit(s) after request deletion.',
            'System'
        FROM @ToDelete td
        INNER JOIN dbo.Request r ON r.ReqId = td.ReqId
        INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
        WHERE i.AffectsInventory = 1;
```

In-transaction (the sproc uses `BEGIN TRAN` + `XACT_ABORT ON`), so this is fail-loud by construction.

- [ ] **Step 2: Verify the SQL parses (sqlcmd/SSMS against a scratch DB, or visual check of BEGIN/TRY nesting)**

- [ ] **Step 3: Commit**

```bash
git add DATABASES/Yakult-DB-Production/dbo/Stored\ Procedures/usp_Request_Delete.sql
git commit -m "audit: trace request deletion stock restores in usp_Request_Delete"
```

---

### Task 8: Set fulfillment upgrades audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\SetRepository.cs:99-235` (inside `ApplySetItemUpgradesAsync` tx loop)

- [ ] **Step 1: Extend the item lookup SELECTs to fetch SerialNumber**

Both `SELECT ItemType, AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId` queries (lines ~99 and ~112) become:

```csharp
using (var cmd = new SqlCommand(@"SELECT ItemType, AffectsInventory, SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn, tx))
```

and add to each reader block (next to `oldItemType` / `newItemType`):

```csharp
string oldSerial = reader[2] == DBNull.Value ? null : Convert.ToString(reader[2]);
```

```csharp
string newSerial = reader[2] == DBNull.Value ? null : Convert.ToString(reader[2]);
```

- [ ] **Step 2: Audit the old item's stock return (IN)**

After the old-item `UPDATE dbo.Item SET StockOnHand = ISNULL(StockOnHand, 0) + @Quantity ...` block (after line ~171), add:

```csharp
ItemAuditTrailWriter.TryLog(conn, tx, new ItemAuditTrailDto
{
    ItemId = up.OldItemId,
    SerialNumber = oldSerial,
    Action = "Item Upgraded - Old Item Returned",
    ActionTime = DateTime.Now,
    Direction = "IN",
    Status = "Completed",
    ReferenceType = "Request",
    ReferenceId = up.ReqId,
    Notes = $"Old item returned to stock after upgrade, qty {up.Quantity}.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 3: Audit the new item's stock deduction (OUT)**

After the new-item stock deduction `UPDATE dbo.Item SET StockOnHand = CASE ...` block (after line ~235), add:

```csharp
ItemAuditTrailWriter.TryLog(conn, tx, new ItemAuditTrailDto
{
    ItemId = up.NewItemId,
    SerialNumber = newSerial,
    Action = "Item Upgraded - New Item Allocated",
    ActionTime = DateTime.Now,
    Direction = "OUT",
    Status = "Completed",
    ReferenceType = "Request",
    ReferenceId = up.ReqId,
    Notes = $"New item allocated as upgrade replacement, qty {up.Quantity}.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 4: Ensure usings exist**

The file already uses `ItemAuditTrailWriter` elsewhere (e.g. set dispatch), but confirm `using Yakult.Inventory.App.Models;` and `using Yakult.Inventory.App.Session;` are present.

- [ ] **Step 5: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Yakult.Inventory.App/Repositories/SetRepository.cs
git commit -m "audit: trace set fulfillment upgrade pullout/allocation"
```

---

### Task 9: Inventory add/edit audit (P1)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\InventoryRepository.cs:35-74` (`CreateInventoryAsync`)
- Modify: `Yakult.Inventory.App\Pages\Inventory\EditInventoryDialog.cs:395-471` (inline tx save handler)

- [ ] **Step 1: Audit inventory entry creation (post-commit, best-effort)**

In `InventoryRepository.CreateInventoryAsync`, replace the final `return (int)returnValue.Value;` with:

```csharp
int invId = (int)returnValue.Value;

try
{
    string direction = null;
    string serial = null;
    using (var auditCon = new SqlConnection(GetConnectionString()))
    {
        await auditCon.OpenAsync();
        using (var cmd = new SqlCommand(@"
SELECT e.EntryType, i.SerialNumber
FROM dbo.Inventory e
LEFT JOIN dbo.Item i ON i.ItemId = e.ItemId
WHERE e.InvId = @InvId", auditCon))
        {
            cmd.Parameters.AddWithValue("@InvId", invId);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    direction = reader.IsDBNull(0) ? null : reader.GetString(0);
                    serial = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }
        }
    }

    var audit = new ItemAuditTrailDto
    {
        ItemId = dto.ItemId,
        SerialNumber = serial,
        Action = "Inventory Entry Added",
        ActionTime = DateTime.Now,
        Direction = direction == "Negative" ? "OUT" : (direction == "Positive" ? "IN" : null),
        Status = "Completed",
        ReferenceType = "Item",
        ReferenceId = dto.ItemId,
        SetCode = dto.SetId?.ToString(),
        Notes = $"Inventory entry {invId} created. EntryType: {direction ?? "?"}, qty {dto.Quantity}.",
        CreatedBy = AppSession.CurrentUserName ?? "System"
    };
    await new ItemAuditTrailRepository().LogActionAsync(audit);
}
catch { /* best-effort post-commit audit */ }

return invId;
```

Add missing usings (`Yakult.Inventory.App.Models`, `Yakult.Inventory.App.Session`) if absent.

- [ ] **Step 2: Audit inventory entry edits (in-tx)**

In `Pages\Inventory\EditInventoryDialog.cs`, inside the tx, after the new-adjustment `ExecuteNonQuery` (after line ~452) and before `transaction.Commit();`, add:

```csharp
string serialNumber = null;
using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, transaction))
{
    cmd.Parameters.AddWithValue("@ItemId", itemItem.Id.Value);
    var result = cmd.ExecuteScalar();
    serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
}

ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
{
    ItemId = itemItem.Id.Value,
    SerialNumber = serialNumber,
    Action = "Inventory Entry Updated",
    ActionTime = DateTime.Now,
    Direction = cmbEntryType.SelectedItem.ToString() == "Positive" ? "IN" : "OUT",
    Status = "Completed",
    ReferenceType = "Item",
    ReferenceId = itemItem.Id.Value,
    Notes = $"Inventory entry {_entry.InvId} updated to EntryType {cmbEntryType.SelectedItem}, qty {quantity}.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

Note: `ItemAuditTrailWriter` is `internal static` in `Yakult.Inventory.App.Repositories` — accessible from this class in the same assembly. Add usings if absent.

- [ ] **Step 3: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Repositories/InventoryRepository.cs Yakult.Inventory.App/Pages/Inventory/EditInventoryDialog.cs
git commit -m "audit: trace inventory ledger add and edit operations"
```

---

### Task 10: Master data bulk edit audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Pages\Admin\Reference-Data\MasterDataUpdatePage.cs:669-687` (bulk-save loop in tx)

- [ ] **Step 1: Audit each updated item in the loop**

Inside the `foreach (var item in dirtyItems)` loop, after `await cmd.ExecuteNonQueryAsync();` (after line ~684), add:

```csharp
ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
{
    ItemId = item.ItemId,
    SerialNumber = item.SerialNumber,
    Action = "Item Bulk Updated",
    ActionTime = DateTime.Now,
    Status = "Completed",
    ReferenceType = "Item",
    ReferenceId = item.ItemId,
    Notes = "Item master data updated via bulk edit.",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 2: Ensure usings exist**

Add `using Yakult.Inventory.App.Models;` and `using Yakult.Inventory.App.Repositories;` if absent.

- [ ] **Step 3: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Pages/Admin/Reference-Data/MasterDataUpdatePage.cs
git commit -m "audit: trace master data bulk item edits"
```

---

### Task 11: Vendor reassignment audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\VendorRepository.cs:188-205` (`UpdateItemVendorAsync`)

- [ ] **Step 1: Audit after the update (post-commit, best-effort)**

In `UpdateItemVendorAsync`, after `int rowsAffected = await cmd.ExecuteNonQueryAsync();` and before `return rowsAffected > 0;`, add:

```csharp
if (rowsAffected > 0)
{
    try
    {
        string serial = null;
        using (var sCon = new SqlConnection(GetConnectionString()))
        {
            await sCon.OpenAsync();
            using (var sCmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", sCon))
            {
                sCmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = await sCmd.ExecuteScalarAsync();
                serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
            }
        }

        await new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
        {
            ItemId = itemId,
            SerialNumber = serial,
            Action = "Item Vendor Updated",
            ActionTime = DateTime.Now,
            Status = "Completed",
            ReferenceType = "Item",
            ReferenceId = itemId,
            Notes = $"Vendor reassigned to VendorId {(vendorId.HasValue ? vendorId.Value.ToString() : "(none)")}.",
            CreatedBy = AppSession.CurrentUserName ?? "System"
        });
    }
    catch { /* best-effort */ }
}
```

Add missing usings (`Yakult.Inventory.App.Models`, `Yakult.Inventory.App.Session`) if absent.

- [ ] **Step 2: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Yakult.Inventory.App/Repositories/VendorRepository.cs
git commit -m "audit: trace item vendor reassignments"
```

---

### Task 12: Renewal date update + renewal archive audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\RenewalRepository.cs:1958-1967` (`UpdateItemDates`)
- Modify: `Yakult.Inventory.App\Repositories\RenewalRepository.cs:1304-1340` (`ArchiveSetItem`)

- [ ] **Step 1: Audit `UpdateItemDates` (post-commit, best-effort)**

Replace the method body so it logs after the update:

```csharp
public void UpdateItemDates(int itemId, DateTime startDate, DateTime endDate)
{
    const string sql = @"
        UPDATE dbo.Item
        SET StartDate = @StartDate,
            EndDate   = @EndDate
        WHERE ItemId = @ItemId";
    using (var connection = new SqlConnection(GetConnectionString()))
        connection.Execute(sql, new { ItemId = itemId, StartDate = startDate, EndDate = endDate });

    try
    {
        string serial = null;
        using (var sCon = new SqlConnection(GetConnectionString()))
        {
            sCon.Open();
            using (var sCmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", sCon))
            {
                sCmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = sCmd.ExecuteScalar();
                serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
            }
        }

        new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
        {
            ItemId = itemId,
            SerialNumber = serial,
            Action = "Item Renewal Dates Updated",
            ActionTime = DateTime.Now,
            Status = "Completed",
            ReferenceType = "Item",
            ReferenceId = itemId,
            Notes = $"Renewal dates set to {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}.",
            CreatedBy = AppSession.CurrentUserName ?? "System"
        }).GetAwaiter().GetResult();
    }
    catch { /* best-effort */ }
}
```

- [ ] **Step 2: Audit `ArchiveSetItem` (in-tx)**

Inside `ArchiveSetItem`, after `connection.Execute(insertRenewalSql, ..., tx);` (line ~1330) and before `tx.Commit();`, add:

```csharp
try
{
    string serial = null;
    using (var sCmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", (SqlConnection)connection, (SqlTransaction)tx))
    {
        sCmd.Parameters.AddWithValue("@ItemId", itemId);
        var result = sCmd.ExecuteScalar();
        serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    ItemAuditTrailWriter.TryLog((SqlConnection)connection, (SqlTransaction)tx, new ItemAuditTrailDto
    {
        ItemId = itemId,
        SerialNumber = serial,
        Action = "Item Renewal Archived",
        ActionTime = DateTime.Now,
        Status = "Completed",
        ReferenceType = "Item",
        ReferenceId = itemId,
        Notes = $"Renewal archived. Reason: {(string.IsNullOrWhiteSpace(reason) ? "(none)" : reason)}.",
        CreatedBy = AppSession.CurrentUserName ?? "System"
    });
}
catch { /* best-effort */ }
```

Note: `connection` and `tx` are Dapper-typed but are actually `SqlConnection`/`SqlTransaction` instances (the method constructs `new SqlConnection(...)` and calls `BeginTransaction()`); the casts are safe.

Add missing usings (`Yakult.Inventory.App.Models`, `Yakult.Inventory.App.Session`, `System.Data.SqlClient`) if absent.

- [ ] **Step 3: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Repositories/RenewalRepository.cs
git commit -m "audit: trace renewal date updates and renewal archives"
```

---

### Task 13: License review classification audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\InvoiceLicenseReviewRepository.cs:164-179` (loop inside `SaveReviewDecisionsAsync` tx)

- [ ] **Step 1: Audit each applied decision in the loop**

Replace the loop body's command execution:

```csharp
using (var command = new SqlCommand(sql, connection, transaction))
{
    command.Parameters.AddWithValue("@ItemId", decision.ItemId);
    command.Parameters.AddWithValue("@Status", decision.Status);
    command.Parameters.AddWithValue("@ReviewedBy", reviewedByUserId);

    int applied = await command.ExecuteNonQueryAsync();
    if (applied > 0)
    {
        string serial = null;
        using (var sCmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", connection, transaction))
        {
            sCmd.Parameters.AddWithValue("@ItemId", decision.ItemId);
            var result = await sCmd.ExecuteScalarAsync();
            serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
        }
        ItemAuditTrailWriter.TryLog(connection, transaction, new ItemAuditTrailDto
        {
            ItemId = decision.ItemId,
            SerialNumber = serial,
            Action = "License Review Classified",
            ActionTime = DateTime.Now,
            Status = "Completed",
            ReferenceType = "Item",
            ReferenceId = decision.ItemId,
            Notes = $"Classified as '{decision.Status}' by reviewer {reviewedByUserId}.",
            CreatedBy = AppSession.CurrentUserName ?? "System"
        });
    }
    totalApplied += applied;
}
```

- [ ] **Step 2: Ensure usings exist**

Add `using Yakult.Inventory.App.Models;` and `using Yakult.Inventory.App.Session;` if absent.

- [ ] **Step 3: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Repositories/InvoiceLicenseReviewRepository.cs
git commit -m "audit: trace license review classification decisions"
```

---

### Task 14: Bulk cellphone details update audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\ItemRepository.cs:1455-1471` (loop inside `UpdateCellPhoneDetails` tx)

- [ ] **Step 1: Audit each updated item in the loop**

After `cmd.ExecuteNonQuery();` inside the `foreach (var item in items)` loop (after line ~1469), add:

```csharp
ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
{
    ItemId = item.ItemId,
    SerialNumber = item.SerialNumber,
    Action = "Cellphone Details Updated",
    ActionTime = DateTime.UtcNow,
    Status = "Completed",
    ReferenceType = "Item",
    ReferenceId = item.ItemId,
    Notes = "Cellphone details updated through bulk edit (name, model, serial, cellphone, IMEI).",
    CreatedBy = AppSession.CurrentUserName ?? "System"
});
```

- [ ] **Step 2: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Yakult.Inventory.App/Repositories/ItemRepository.cs
git commit -m "audit: trace bulk cellphone details updates"
```

---

### Task 15: FulfillRequest audit (P2)

**Files:**
- Modify: `Yakult.Inventory.App\Repositories\RequestRepository.cs:1518-1545` (`FulfillRequest`)

- [ ] **Step 1: Audit after the request update (post-commit, best-effort)**

In `FulfillRequest`, after `ActivityLogger.Log(...)` (line ~1543-1544), add:

```csharp
try
{
    int itemId = 0;
    string serial = null;
    using (var qCon = new SqlConnection(GetConnectionString()))
    {
        qCon.Open();
        using (var qCmd = new SqlCommand(@"
SELECT r.ItemId, i.SerialNumber
FROM dbo.Request r
LEFT JOIN dbo.Item i ON i.ItemId = r.ItemId
WHERE r.ReqId = @ReqId", qCon))
        {
            qCmd.Parameters.AddWithValue("@ReqId", reqId);
            using (var reader = qCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    itemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    serial = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }
        }
    }

    await new ItemAuditTrailRepository().LogActionAsync(new ItemAuditTrailDto
    {
        ItemId = itemId > 0 ? (int?)itemId : null,
        SerialNumber = serial,
        Action = "Request Fulfilled",
        ActionTime = DateTime.Now,
        Direction = "OUT",
        Status = "Completed",
        ReferenceType = "Request",
        ReferenceId = reqId,
        Notes = $"Issued {additionalIssuedQty} more unit(s) for Request #{reqId}.",
        CreatedBy = AppSession.CurrentUserName ?? "System"
    });
}
catch { /* best-effort */ }
```

- [ ] **Step 2: Build**

Run the Task 1 Step 4 command. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Yakult.Inventory.App/Repositories/RequestRepository.cs
git commit -m "audit: trace request fulfillment (issued quantity)"
```

---

### Task 16: Final verification

- [ ] **Step 1: Full build**

Run:
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Yakult.Inventory.App\Yakult.Inventory.App.csproj" /t:Build /p:Configuration=Debug /v:minimal /nologo
```
Expected: `Build succeeded`, 0 errors, 0 warnings.

- [ ] **Step 2: No new csproj entries needed**

Run the duplicate/ghost audit from CLAUDE.md (must both print 0):

```powershell
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
$all = [regex]::Matches($csproj, '(?:Compile|Page|EmbeddedResource|None)\s+Include="([^"]+)"')
$dups = $all | Group-Object { $_.Groups[1].Value } | Where-Object { $_.Count -gt 1 }
Write-Host "Duplicates: $($dups.Count)"
$pageEntries = [regex]::Matches($csproj, 'Compile Include="(Pages[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$ghosts = $pageEntries | Where-Object { -not (Test-Path "Yakult.Inventory.App\$_") }
Write-Host "Ghost entries: $($ghosts.Count)"
```

- [ ] **Step 3: SQL spot-check (manual, on a scratch DB)**

Verify each new audit action fires by running the flow then:

```sql
SELECT Action, COUNT(*) AS Cnt
FROM dbo.ItemAuditTrail
WHERE Action IN (
    'Item Sold', 'Item Disposed', 'Item Restored', 'Item Archived', 'Item Archived - Deactivated',
    'Invoice Line Deleted - Stock Restored', 'Invoice Deleted - Stock Restored',
    'Request Deleted - Stock Restored', 'Item Upgraded - Old Item Returned',
    'Item Upgraded - New Item Allocated', 'Inventory Entry Added', 'Inventory Entry Updated',
    'Item Bulk Updated', 'Item Vendor Updated', 'Item Renewal Dates Updated',
    'Item Renewal Archived', 'License Review Classified', 'Cellphone Details Updated',
    'Request Fulfilled'
)
GROUP BY Action
ORDER BY Action;
```
Expected: one or more rows per exercised flow; no NULL/empty Action rows introduced.

- [ ] **Step 4: Commit any remaining files**

```bash
git status --short
```

Commit anything unexpected only after reviewing it. If clean, nothing to commit.

---

## Self-Review

**Spec coverage:** Every gap from the analysis is covered: Sell/Dispose (T1), invoice line/invoice deletes (T2/T3), WPF grid archive (T4), legacy sproc archive (T5), restore dialogs (T6), request delete sproc (T7), set upgrades (T8), inventory add/edit (T9), master data (T10), vendor reassignment (T11), renewals (T12), license review (T13), bulk cellphone (T14), FulfillRequest (T15). Cartridge and web portal deliberately excluded per user scope.

**Placeholder scan:** No TBDs. All SQL and C# snippets are complete and reference existing symbols (`ItemAuditTrailWriter.TryLog`, `ItemAuditTrailRepository.LogActionAsync`, `ItemAuditTrailDto`, `AppSession`, `GetConnectionString`).

**Type consistency:** `ItemAuditTrailDto` properties used match `Models\ItemAuditTrailDto.cs`. `TryLog(SqlConnection, SqlTransaction, ItemAuditTrailDto)` matches `ItemAuditTrailWriter.cs:9-12`. `LogActionAsync(ItemAuditTrailDto)` matches `ItemAuditTrailRepository.cs:29`. Action strings are defined once per task and reused consistently.

**Known risk:** Task 12 casts Dapper's `IDbConnection`/`IDbTransaction` to `SqlConnection`/`SqlTransaction`. Both are guaranteed to be those concrete types in `RenewalRepository` (it constructs them directly), and `ItemAuditTrailWriter.TryLog` requires the concrete types — this is the established pattern in `CallMonitoringRepository` (which passes the same concrete types into `TryLogWithResult`).
