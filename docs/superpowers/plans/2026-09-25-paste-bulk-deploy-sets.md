# Paste Bulk Deploy Sets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a paste-grid bulk deploy flow on View Sets that turns clipboard rows into 1 Set per ComputerName with full Item plus Request plus Set plus audit history, with zero git operations and zero database schema changes.

**Architecture:** Staging WPF window launched from the Sets action bar reuses the anchored-paste logic from BatchAddItemDialog, validates rows in memory with batched read-only DB lookups, then commits one transaction per BundleKey through the existing SetRepository chain (CreateSetAsync plus AddRequestToSetAsync plus DispatchSetAsync). No new tables, no migrations, no git commands.

**Tech Stack:** .NET Framework 4.8 WPF plus WinForms interop, ADO.NET SqlClient, Excel clipboard TSV (tab plus CRLF), existing repositories only.

**Locked user decisions (do not re-ask):** 1 Set per ComputerName; Department always per row (no lock mode); blank DateDeploy means NULL DispatchDate (Set stays Pending); existing serial in DB means reuse Item and link (yellow warning); conflicts shown red inline plus end-of-run log; commit scope valid-rows-only; blank monitor serial means CPU-only Set with `Monitor pending` remark for later follow-up; anyone with Add-Set rights may use it; FixedAssetNumber goes to Item.Description plus Request.Remarks (no new column); GA11 ComputerName collision left for manual rename; no GitHub operations; no database changes.

**Constraints (hard):** No `git add`, `git commit`, `git push`, PR, or branch commands. No `ALTER TABLE`, `CREATE TABLE`, migration scripts, or direct data edits in PROD or DEV. Every new `.cs` file must get a `<Compile Include>` entry in `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (non-SDK-style project). No `--` inside any XML comment. Run the Layer-7 conflict check and final csproj verification from AGENTS.md before finishing.

---

### Task 1: Paste parser (pure, no DB)

**Files:**
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployParser.cs`
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployRow.cs`

- [ ] **Step 1: Create BulkDeployRow DTO**

```csharp
namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public sealed class BulkDeployRow
    {
        public string BundleKey { get; set; }
        public string Department { get; set; }
        public string ComputerName { get; set; }
        public string IPAddress { get; set; }
        public string ItemRole { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string FixedAssetNumber { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; } = 1;
        public string DateDeployedText { get; set; }
        public string Condition { get; set; } = "Good";
        public string Vendor { get; set; }
        public string Remarks { get; set; }
        public string RowStatus { get; set; } = "Pending";
        public string RowMessage { get; set; } = "";
    }
}
```

- [ ] **Step 2: Create BulkDeployParser with two source formats**

```csharp
using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class BulkDeployParser
    {
        public static List<BulkDeployRow> ParseClipboardText(string text, bool includeDate, bool includeComputerName, bool includeIp, bool includeDeptCol, bool includeFixedAsset)
        {
            var outRows = new List<BulkDeployRow>();
            if (string.IsNullOrEmpty(text)) return outRows;
            var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = line.Split('\t');
                for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                if (cells.Length >= 12 && IsLegacyYpiRow(cells))
                    outRows.AddRange(SplitLegacyRow(cells, includeDate, includeComputerName, includeIp, includeDeptCol, includeFixedAsset));
                else
                    outRows.Add(MapTemplateRow(cells, includeDate, includeComputerName, includeIp, includeDeptCol, includeFixedAsset));
            }
            return outRows;
        }

        private static bool IsLegacyYpiRow(string[] cells)
        {
            return cells.Length == 12 || cells.Length == 16;
        }

        private static IEnumerable<BulkDeployRow> SplitLegacyRow(string[] c, bool incDate, bool incPc, bool incIp, bool incDept, bool incFa)
        {
            string dept = incDept ? c[1] : "";
            string pc = incPc ? c[3] : "";
            string ip = incIp ? c[4] : "";
            string date = incDate ? c[11] : "";
            string fa = incFa ? c[7] : "";
            yield return new BulkDeployRow
            {
                BundleKey = string.IsNullOrWhiteSpace(pc) ? Guid.NewGuid().ToString("N") : pc.Trim(),
                Department = dept, ComputerName = pc, IPAddress = ip,
                ItemRole = "CPU", ItemName = c[2], ModelNumber = c[6],
                SerialNumber = c[5], FixedAssetNumber = fa, Category = "Desktop",
                Quantity = 1, DateDeployedText = date, Condition = "Good"
            };
            if (!string.IsNullOrWhiteSpace(c[9]))
            {
                yield return new BulkDeployRow
                {
                    BundleKey = string.IsNullOrWhiteSpace(pc) ? Guid.NewGuid().ToString("N") : pc.Trim(),
                    Department = dept, ComputerName = pc, IPAddress = ip,
                    ItemRole = "Monitor", ItemName = c[8], ModelNumber = c[10],
                    SerialNumber = c[9], FixedAssetNumber = "", Category = "Monitor",
                    Quantity = 1, DateDeployedText = date, Condition = "Good"
                };
            }
        }

        private static BulkDeployRow MapTemplateRow(string[] c, bool incDate, bool incPc, bool incIp, bool incDept, bool incFa)
        {
            string Get(int i) { return i < c.Length ? c[i] : ""; }
            int qty = 1;
            int.TryParse(Get(10), out qty);
            if (qty < 1) qty = 1;
            if (qty > 3) qty = 3;
            return new BulkDeployRow
            {
                BundleKey = Get(0),
                Department = incDept ? Get(1) : "",
                ComputerName = incPc ? Get(2) : "",
                IPAddress = incIp ? Get(3) : "",
                ItemRole = Get(4), ItemName = Get(5), ModelNumber = Get(6),
                SerialNumber = Get(7), FixedAssetNumber = incFa ? Get(8) : "",
                Category = Get(9), Quantity = qty,
                DateDeployedText = incDate ? Get(11) : "",
                Condition = string.IsNullOrWhiteSpace(Get(12)) ? "Good" : Get(12),
                Vendor = Get(13), Remarks = Get(14)
            };
        }
    }
}
```

- [ ] **Step 3: Build the project to verify Task 1 compiles**

Run: `msbuild Yakult.Inventory.App\Yakult.Inventory.App.csproj /p:Configuration=Debug /p:Platform=x86 /t:Build /v:minimal`
Expected: Build succeeds (new files not yet registered in csproj will fail here, which tells you to do Task 5 step 1 next, then rebuild).

---

### Task 2: Validator plus department alias map (read-only DB lookups only)

**Files:**
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\DeptAliasMap.cs`
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployValidator.cs`

- [ ] **Step 1: Create DeptAliasMap with the exact canonical map**

```csharp
using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class DeptAliasMap
    {
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Audit Department", "Audit" }, { "Audit", "Audit" },
            { "Accounting", "Accounting" }, { "Acctg", "Accounting" },
            { "Credit and Collection", "Credit and Collection" }, { "Credit", "Credit and Collection" },
            { "Direct Sales", "Direct Sales" },
            { "PMD", "PMD" }, { "PDD", "PDD" },
            { "Gen. Affairs", "Gen. Affairs" }, { "GA", "Gen. Affairs" },
            { "PRSD", "PRSD" }, { "Finance", "Finance" }, { "Shipping", "Shipping" },
            { "Purchasing", "Purchasing" }, { "Engineering", "Engineering" },
            { "Treasury", "Treasury" }, { "MPD", "MPD" }, { "Legal", "Legal" },
            { "Materials", "Materials" }, { "MATERIALS", "Materials" },
            { "MATERIALS1", "Materials" }, { "MATERIALS2", "Materials" },
            { "MATERIALS3", "Materials" }, { "MATERIALS6", "Materials" },
            { "MATERIALS7", "Materials" }, { "MAT7", "Materials" },
            { "PERSONNEL", "Personnel" }, { "Personnel", "Personnel" },
            { "ITD7", "IT" }, { "Storage", "Storage" }
        };

        public static bool TryCanonicalize(string raw, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return Aliases.TryGetValue(raw.Trim(), out canonical);
        }
    }
}
```

- [ ] **Step 2: Create BulkDeployValidator (in-memory plus batched reads, no writes)**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public sealed class BulkDeployValidationResult
    {
        public bool IsError { get; set; }
        public bool IsWarning { get; set; }
        public string Message { get; set; } = "";
        public string CanonicalDept { get; set; }
        public bool ReusesExistingItem { get; set; }
        public DateTime? ParsedDate { get; set; }
    }

    public static class BulkDeployValidator
    {
        private static readonly HashSet<string> ValidRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CPU", "Monitor", "Laptop", "Printer", "Keyboard", "Mouse", "UPS", "Charger", "Dock", "Other" };
        private static readonly HashSet<string> ValidConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Good", "Damaged" };

        public static BulkDeployValidationResult ValidateRow(
            BulkDeployRow row,
            HashSet<string> serialsInBatch,
            HashSet<string> existingSerialsInDb,
            Dictionary<string, string> computerToBundle)
        {
            var r = new BulkDeployValidationResult();
            var notes = new List<string>();
            if (string.IsNullOrWhiteSpace(row.BundleKey)) { r.IsError = true; notes.Add("BundleKey required."); }
            if (string.IsNullOrWhiteSpace(row.ItemName)) { r.IsError = true; notes.Add("ItemName required."); }
            if (string.IsNullOrWhiteSpace(row.Category)) { r.IsError = true; notes.Add("Category required."); }
            if (!ValidRoles.Contains(row.ItemRole ?? "")) { r.IsError = true; notes.Add("ItemRole must be CPU, Monitor, Laptop, Printer, Keyboard, Mouse, UPS, Charger, Dock, or Other."); }
            if (row.Quantity < 1 || row.Quantity > 3) { r.IsError = true; notes.Add("Quantity must be 1 to 3."); }
            if (!string.IsNullOrWhiteSpace(row.Condition) && !ValidConditions.Contains(row.Condition)) { r.IsError = true; notes.Add("Condition must be Good or Damaged."); }
            if (string.IsNullOrWhiteSpace(row.SerialNumber)) { r.IsWarning = true; notes.Add("Blank serial: CPU-only or pending follow-up unit."); }
            else
            {
                if (!serialsInBatch.Add(row.SerialNumber.Trim())) { r.IsError = true; notes.Add("Duplicate serial " + row.SerialNumber.Trim() + " inside this paste."); }
                else if (existingSerialsInDb.Contains(row.SerialNumber.Trim())) { r.IsWarning = true; r.ReusesExistingItem = true; notes.Add("Serial " + row.SerialNumber.Trim() + " already in DB: Item will be reused and linked."); }
            }
            string canonical;
            if (!DeptAliasMap.TryCanonicalize(row.Department, out canonical)) { r.IsError = true; notes.Add("Unknown department '" + row.Department + "'."); }
            else r.CanonicalDept = canonical;
            if (!string.IsNullOrWhiteSpace(row.ComputerName))
            {
                string first;
                if (computerToBundle.TryGetValue(row.ComputerName.Trim(), out first))
                {
                    if (!string.Equals(first, row.BundleKey, StringComparison.OrdinalIgnoreCase)) { r.IsError = true; notes.Add("ComputerName " + row.ComputerName.Trim() + " already used in bundle " + first + " (rename one)."); }
                }
                else computerToBundle[row.ComputerName.Trim()] = row.BundleKey ?? "";
            }
            if (!string.IsNullOrWhiteSpace(row.IPAddress))
            {
                IPAddress addr;
                if (!IPAddress.TryParse(row.IPAddress.Trim(), out addr)) { r.IsError = true; notes.Add("IPAddress '" + row.IPAddress + "' is not valid IPv4."); }
            }
            if (!string.IsNullOrWhiteSpace(row.DateDeployedText))
            {
                DateTime d;
                double oa;
                if (DateTime.TryParse(row.DateDeployedText.Trim(), out d)) r.ParsedDate = d.Date;
                else if (double.TryParse(row.DateDeployedText.Trim(), out oa))
                {
                    try { r.ParsedDate = DateTime.FromOADate(oa).Date; }
                    catch { r.IsError = true; notes.Add("DateDeployed '" + row.DateDeployedText + "' not parseable."); }
                }
                else { r.IsError = true; notes.Add("DateDeployed '" + row.DateDeployedText + "' not parseable (use yyyy-mm-dd)."); }
            }
            else r.ParsedDate = null;
            r.Message = string.Join(" ", notes);
            return r;
        }
    }
}
```

- [ ] **Step 3: Rebuild to verify Tasks 1 plus 2 compile (after csproj registration in Task 5)**

Run: `msbuild Yakult.Inventory.App\Yakult.Inventory.App.csproj /p:Configuration=Debug /p:Platform=x86 /t:Build /v:minimal`
Expected: PASS once `<Compile Include>` entries exist.

---

### Task 3: Staging window with column switchboard plus anchored paste

**Files:**
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployWindow.xaml`
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployWindow.xaml.cs`
- Create: `Yakult.Inventory.App\Wpf\Set\BulkDeploy\BulkDeployViewModel.cs`

- [ ] **Step 1: Create BulkDeployWindow.xaml with Step 1 switchboard plus Step 2 grid**

Step 1 checkboxes (all checked by default, the YPI 2025 preset): `ChkDate (Deployment Date)`, `ChkPc (ComputerName)`, `ChkIp (IP Address)`, `ChkDept (Department column, always checked per user decision, disabled to prevent uncheck)`, `ChkFa (Fixed Asset #)`. Preset buttons: `YPI 2025 file (all on)` and `Minimal (Date plus IP plus Fa off)`. Step 2: `DataGrid BulkGrid` bound to `Rows` with columns BundleKey, Department, ComputerName, IPAddress, ItemRole, ItemName, ModelNumber, SerialNumber, FixedAssetNumber, Category, Quantity, DateDeployedText, Condition, Vendor, Remarks, RowStatus (read-only), RowMessage (read-only). Buttons: `Paste (Ctrl+V)`, `Delete selected cells (Del)`, `Validate`, `Create valid only`, `Cancel`. Progress bar plus result log TextBox. Column visibility binds to the five checkboxes so unchecked columns hide and are excluded from paste mapping. Do not use `LetterSpacing` or any property from the AGENTS.md WPF ban list. Do not put `--` inside any XML comment.

- [ ] **Step 2: Create BulkDeployWindow.xaml.cs with anchored paste ported from BatchAddItemDialog**

Port `Pages/Item/BatchAddItemDialog.xaml.cs:864 PasteCellPhoneRowsFromClipboard` exactly: `CommitEdit Cell plus Row` before reading anchor, anchor on top-left of `SelectedCells` falling back to `CurrentCell`, map clipboard tab-columns onto consecutive visible editable grid columns ordered by `DisplayIndex`, `Split('\t')` per line, `Trim()` each value, ignore rows past the end of the backing list, call `RefreshView` after. Add `PreviewKeyDown` for Ctrl+V and Delete (skip Delete when focus is inside a TextBox editor, same guard as the source). Add right-click anchor fix from `PreviewMouseRightButtonDown`. Paste calls `BulkDeployParser.ParseClipboardText` with the five switchboard flags, appends to `Rows`, then runs validator and paints `RowStatus` green/yellow/red.

- [ ] **Step 3: Create BulkDeployViewModel with batched DB pre-reads (reads only)**

```csharp
public async Task ValidateAllAsync()
{
    var serials = Rows.Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber)).Select(r => r.SerialNumber.Trim()).Distinct().ToList();
    var existing = await new SetBulkDeployRepository().GetExistingSerialsAsync(serials);
    var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var pcMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var row in Rows)
    {
        var res = BulkDeployValidator.ValidateRow(row, batch, existing, pcMap);
        row.RowStatus = res.IsError ? "Error" : (res.IsWarning ? "Warning" : "Valid");
        row.RowMessage = res.Message;
    }
}
```

Department existence is confirmed by `DeptAliasMap` plus one `SELECT DeptId, Name FROM dbo.Department` read; unmatched canonical names become errors (never auto-created). Vendor, when supplied, is checked with `SELECT TOP 1 VendorID FROM dbo.Vendor WHERE VendorName=@Name AND IsActive=1` and blanked with a warning when missing (same as `InvoiceCsvImportDialog.ResolveVendorId`).

---

### Task 4: Commit repository (reuses existing chain, one transaction per bundle)

**Files:**
- Create: `Yakult.Inventory.App\Repositories\SetBulkDeployRepository.cs`

- [ ] **Step 1: Create SetBulkDeployRepository with read helpers plus per-bundle commit**

Read helpers: `GetExistingSerialsAsync(List<string>)` (one batched `WHERE SerialNumber IN`, returns HashSet), `GetDepartmentIdsAsync()` (reads `DeptId plus Name`), `GetComputerNamesAsync(List<string>)` (one batched read of `Set.ComputerName` for skip-detection). Commit method signature:

```csharp
public async Task<BulkDeployResult> CreateDeployedSetsAsync(
    List<BulkDeployRow> validRows,
    Dictionary<string, DateTime?> bundleDates,
    int createdByUserId)
```

Per BundleKey (BundleKey equals ComputerName in the 1-Set-per-ComputerName rule): open connection, begin transaction, find-or-create each Item by trimmed serial (new rows: `ItemType='Hardware'`, `StockOnHand=1`, `AffectsInventory=1`, `FixedAssetNumber` appended to `Description` plus `Remarks`, never a new column), insert dept-level `Request` rows directly (`EmpId=NULL`, `DeptId` from canonical map, `Quantity=1`, `EntryType='Negative'`, `Status='Completed'`, `IssuedQty=1`), `CreateSetAsync(createdBy, remarks, dispatchDate NULL when blank, status Pending when date NULL else Dispatched)`, `AddRequestToSetAsync` per request, `UPDATE dbo.[Set] SET ComputerName=@Pc, IPAddress=@Ip WHERE SetId=@Id`, `DispatchSetAsync` only when a date was supplied (NULL date means leave Pending), `ItemAuditTrail` writes for Item Created, Request Created, Set Dispatched. Blank monitor serial means that unit row is skipped at commit with a `Monitor pending` remark on the Set (CPU-only Set). Existing `ComputerName` in DB means skip whole bundle with `Skipped - already imported`. Existing serial in DB means reuse that `ItemId`. Any exception rolls back only that bundle; collect per-bundle `SetCode`, `Skipped`, or `Failed: message`. No `SubmitRequest` call (its INNER JOIN Employee rejects dept-level rows). No new tables. No migrations.

- [ ] **Step 2: Verify commit logic builds**

Run: `msbuild Yakult.Inventory.App\Yakult.Inventory.App.csproj /p:Configuration=Debug /p:Platform=x86 /t:Build /v:minimal`
Expected: PASS.

---

### Task 5: Wire button, register files, verify (no git, no DB)

**Files:**
- Modify: `Yakult.Inventory.App\Wpf\Set\Views\SetPageView.xaml` (add `Paste Bulk Deploy` button after Bulk Add Files at line 293)
- Modify: `Yakult.Inventory.App\Wpf\Set\Views\SetPageView.xaml.cs` (open BulkDeployWindow with SetsGrid owner, reload on close)
- Modify: `Yakult.Inventory.App\Wpf\Set\ViewModels\SetPageViewModel.cs` (add `BulkDeployCommand` plus `RequestBulkDeploy` event)
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (add `<Compile Include>` for the 6 new files, no SubType for helpers, SubType Form or UserControl only where the class extends it)

- [ ] **Step 1: Register the 6 new files in the csproj inside the matching folder blocks**

```xml
<Compile Include="Repositories\SetBulkDeployRepository.cs" />
<Compile Include="Wpf\Set\BulkDeploy\BulkDeployParser.cs" />
<Compile Include="Wpf\Set\BulkDeploy\BulkDeployValidator.cs" />
<Compile Include="Wpf\Set\BulkDeploy\DeptAliasMap.cs" />
<Compile Include="Wpf\Set\BulkDeploy\BulkDeployRow.cs" />
<Compile Include="Wpf\Set\BulkDeploy\BulkDeployViewModel.cs" />
```

Plus `Page` entries for the Window XAML pair following the existing Wpf Page pattern in the same file. Validate XML after edit:

Run: `try { [xml](Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw) | Out-Null; Write-Host "VALID XML" } catch { Write-Host "INVALID: $($_.Exception.Message)" }`
Expected: `VALID XML`.

- [ ] **Step 2: Run Layer-7 conflict check (must print 0)**

Run: `$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw; $entries = [regex]::Matches($csproj, 'Compile Include="([^"]+\.cs)"') | ForEach-Object { $_.Groups[1].Value }; $conflicts = $entries | Where-Object { $_ -notmatch '\.xaml\.cs$' -and $_ -notmatch '\.Designer\.cs$' -and ($entries -contains ($_ -replace '\.cs$', '.xaml.cs')) }; Write-Host "Conflicts: $($conflicts.Count)"; $conflicts`
Expected: `Conflicts: 0`.

- [ ] **Step 3: Run final csproj verification (both must print 0)**

Run: `$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw; $all = [regex]::Matches($csproj, '(?:Compile|Page|EmbeddedResource|None)\s+Include="([^"]+)"'); $dups = $all | Group-Object { $_.Groups[1].Value } | Where-Object { $_.Count -gt 1 }; Write-Host "Duplicates: $($dups.Count)"; $pageEntries = [regex]::Matches($csproj, 'Compile Include="(Pages[^"]+)"') | ForEach-Object { $_.Groups[1].Value }; $ghosts = $pageEntries | Where-Object { -not (Test-Path "Yakult.Inventory.App\$_") }; Write-Host "Ghost entries: $($ghosts.Count)"`
Expected: `Duplicates: 0` and `Ghost entries: 0`.

- [ ] **Step 4: Full build plus manual dry-run (backup DB copy, never live)**

Run: `msbuild Yakult.Inventory.App.sln /p:Configuration=Debug /p:Platform=x86 /t:Build /v:minimal`
Expected: BUILD SUCCEEDED, 0 errors. Then paste 5 rows (1 normal PC plus monitor, 1 blank monitor, 1 blank date, 1 duplicate serial, 1 bad dept) into the staging window on a backup database copy and confirm: 2 Sets created, 1 warning, 1 NULL-date Pending Set, 1 red duplicate blocked, 1 red dept blocked, View Items plus View Requests plus View Sets all show the new history, result log lists per-bundle outcomes. Do not run this against production.

---

## Self-Review

Spec coverage: 1-Set-per-ComputerName in Task 4 bundle loop; per-row dept in Task 2 alias map plus Task 4 insert; NULL blank dates in Task 2 ParsedDate null plus Task 4 Pending path; serial reuse in Task 2 ReusesExistingItem plus Task 4 find-or-create; conflict display in Task 3 RowStatus plus Task 4 result log; valid-only commit in Task 4 per-bundle transactions; CPU-only blank monitors in Task 1 SplitLegacyRow plus Task 4 skip; open permissions in Task 5 button with existing Add-Set rights; FixedAsset to Description/Remarks in Task 4; no-git and no-DB constraints stated in header and Task 5 (all verification is local build plus backup-DB dry run).

No placeholders, no TBD, all signatures and file paths concrete and consistent across tasks (BulkDeployRow fields match parser, validator, ViewModel, and repository parameters).
