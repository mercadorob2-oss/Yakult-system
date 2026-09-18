# E-Documents Section (Report Monitoring) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an "E-Documents" entry to the desktop REPORT MONITORING side menu that hosts Transmittal, Gatepass, and Requisition documents in one modern, responsive WPF dashboard.

**Architecture:** New WinForms host page `Pages\Reports\EDocumentsPage.cs` (ElementHost pattern, exactly like `Pages\Set\ViewSetPage.cs`) embeds a new WPF `EDocsDashboardView` with three tabs. The Transmittal and Requisition tabs reuse the existing print views/services (`CartridgeTransmittalPrintView`, `RequisitionFormPrintView`); only Gatepass is built new, mirroring the mobile `GatepassReport` field set. Menu wiring follows the existing `ReportsForm.cs` bottom-to-top pattern with a `PermissionResolver.HasPageAccess("EDocumentsPage")` guard, backed by a data-only permission seed migration.

**Tech Stack:** .NET Framework WinForms + WPF (XAML, `ElementHost`), T-SQL migration scripts, MSBuild.

**Reference inputs (already analyzed, do not re-analyze):**
- Mobile gatepass format: `Latest_sys/YakultScanner/app/src/main/java/com/example/yakultscanner/utils/GatepassPrintUtils.kt` (Legal page, 2 copies/page, 12 item lines; fields: company YPI/YMC checkboxes, TO/FROM/DATE, item category checkboxes incl. COMPUTER TABLE FIXED ASSET NO., model LX300/LX310, FIXED ASSET NO., QUANTITY, OTHERS, REMARKS, ISSUED BY / NOTED BY / RECEIVED BY-DATE / APPROVED BY-DATE).
- Mobile transmittal format: `.../utils/TransmittalPrintUtils.kt` (Letter portrait, TRANSMITTAL + FILE copies; TO/DATE/FROM, NO. + DESCRIPTION/PARTICULARS table, PREPARED / RECEIVED-DATE / TRANSMITTED / APPROVED-DATE / NOTED BY).
- Requisition xlsx (`FORMS (REQUISITION FORM).xlsx`): ~600 sheets, one filled form per sheet, two identical copies per sheet (rows 1-27 + 34-60); cell map: A1 company check, A4 title, D7 department, H7 DATE label, J7 date, A9/D9/J9 headers, A12/D12/J12 item start (remarks wrap), row 24 signature labels, row 26 names, row 27 positions; some sheets label the first signer REQUESTED BY (2022 sheets), newer ones PREPARED BY (desktop uses PREPARED BY — keep it).

---

### Task 1: Permission seed migration for the new page

**Files:**
- Create: `DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_PermissionItem_SeedEDocumentsPage.sql`
- Reference: `DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_PageNavEntry_CreateAndSeed.sql:45-57` (seed pattern), `Migration_PermissionItem_SeedNavPages.sql:58` (PermissionItem row pattern)

- [ ] **Step 1: Inspect the PermissionItem seed row pattern**

Run: `Select-String -Path "DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_PermissionItem_SeedNavPages.sql" -Pattern "OutboundBatchesPage"`
Expected: one row showing the exact column list for a Page-type PermissionItem (copy its column order verbatim).

- [ ] **Step 2: Write the migration file**

Create `DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_PermissionItem_SeedEDocumentsPage.sql` with exactly two idempotent blocks (copy the IF NOT EXISTS guard style from `Migration_ITCM_CallTicket_AddTicketSource.sql:7-21`):
1. Insert `dbo.PermissionItem` row for ItemKey `EDocumentsPage` (same columns/values shape as the `OutboundBatchesPage` row found in Step 1, only the key/name changed to E-Documents).
2. Insert `dbo.PageNavEntry` row `('EDocumentsPage', 'Reports', 'Report Monitoring', 'E-Documents', 7)` guarded by `IF NOT EXISTS (SELECT 1 FROM dbo.PageNavEntry WHERE PermissionItemId = @Id AND PortalKey = 'Reports' AND MenuGroup = 'Report Monitoring')`, resolving `@Id` via `SELECT PermissionItemId FROM dbo.PermissionItem WHERE ItemKey = 'EDocumentsPage'`.

- [ ] **Step 3: Apply to DEV and verify rows exist**

Run (PowerShell, splits on GO):
```powershell
$sql = Get-Content "DATABASES\Yakult-DB-Production\dbo\Scripts\Migration_PermissionItem_SeedEDocumentsPage.sql" -Raw
foreach ($db in @("Yakult_Inventory_System_DEV")) {
  $cs = "Server=192.168.100.186,50301;Database=$db;User ID=remote_user;Password=Yakult-ITD;TrustServerCertificate=True;Encrypt=False;"
  $c = New-Object System.Data.SqlClient.SqlConnection($cs); $c.Open()
  foreach ($b in ($sql -split "(?m)^GO\s*$" | Where-Object { $_.Trim() -ne "" })) {
    $cmd = $c.CreateCommand(); $cmd.CommandText = $b; $cmd.CommandTimeout = 120; $cmd.ExecuteNonQuery() | Out-Null
  }
  $chk = $c.CreateCommand(); $chk.CommandText = "SELECT i.ItemKey, n.MenuGroup, n.DisplayName, n.SortOrder FROM dbo.PermissionItem i INNER JOIN dbo.PageNavEntry n ON n.PermissionItemId = i.PermissionItemId WHERE i.ItemKey = 'EDocumentsPage'"
  $a = New-Object System.Data.SqlClient.SqlDataAdapter($chk); $t = New-Object System.Data.DataTable; [void]$a.Fill($t); $t | Format-Table -AutoSize | Out-String -Width 200
  $c.Close()
}
```
Expected: one row `EDocumentsPage | Report Monitoring | E-Documents | 7`. (PROD deploy happens with the release, not in this task.)

- [ ] **Step 4: Commit**

```bash
git add DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_PermissionItem_SeedEDocumentsPage.sql
git commit -m "feat(edocs): seed EDocumentsPage permission + Report Monitoring nav entry"
```

---

### Task 2: "E-Documents" menu entry in ReportsForm

**Files:**
- Modify: `Yakult.Inventory.App/Forms/Reports/ReportsForm.cs` (insert after the Cartridge Disposed/Sold block, lines 207-214)

- [ ] **Step 1: Add the menu button (appears below Cartridge, above Back to Portal)**

Insert directly after the `// ── CARTRIDGE DISPOSED / SOLD` block's `Spacer(5);` (line 214), keeping the bottom-to-top rule (code order = reverse visual order):
```csharp
// ── E-DOCUMENTS ──────────────────────────────────────────────────
_sideMenuPanel.Controls.Add(AddMenuButton("E-Documents", (s, e) =>
{
    ShowEDocumentsPage();
    ToggleMenu();
}));

_sideMenuPanel.Controls.Add(Spacer(5));
```

- [ ] **Step 2: Add the navigation method next to ShowCartridgeDisposedSoldPage (line 415)**

```csharp
private void ShowEDocumentsPage()
{
    if (!PermissionResolver.HasPageAccess("EDocumentsPage"))
    {
        MessageBox.Show("Access denied. You do not have permission to view this page.",
            "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    ShowPage(new Yakult.Inventory.App.Pages.Reports.EDocumentsPage());
}
```

- [ ] **Step 3: Verify compile of this file only (fast syntax gate before the page exists — EXPECTED TO FAIL)**

Run: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /t:library /nologo /out:$env:TEMP\edocs_gate.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "Yakult.Inventory.App\Forms\Reports\ReportsForm.cs"`
Expected: FAIL with `error CS0246: The type or namespace name 'EDocumentsPage' could not be found` and nothing else (proves wiring is correct and the only missing piece is Task 3). Do NOT fix anything here; proceed to Task 3.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Forms/Reports/ReportsForm.cs
git commit -m "feat(edocs): add E-Documents menu entry to Report Monitoring"
```

---

### Task 3: WinForms host page (ElementHost + WPF dashboard)

**Files:**
- Create: `Yakult.Inventory.App/Pages/Reports/EDocumentsPage.cs`
- Create: `Yakult.Inventory.App/Pages/Reports/EDocumentsPage.Designer.cs`
- Reference: `Yakult.Inventory.App/Pages/Set/ViewSetPage.cs` (copy its ElementHost pattern verbatim: private ElementHost field, Dock Fill, Child = WPF view)
- Modify: `Yakult.Inventory.App/Yakult.Inventory.App.csproj` (register both files; see Task 3 Step 3)

- [ ] **Step 1: Read the reference host page**

Read `Yakult.Inventory.App/Pages/Set/ViewSetPage.cs` in full (it is small). Copy its structure exactly: namespace `Yakult.Inventory.App.Pages.Reports`, class `EDocumentsPage : UserControl`, constructor creates `WPF.EDocs.Views.EDocsDashboardView`, wraps in `ElementHost { Dock = DockStyle.Fill }`, disposes host in `Dispose(bool)`.

- [ ] **Step 2: Write EDocumentsPage.cs + EDocumentsPage.Designer.cs**

`EDocumentsPage.Designer.cs` contains only `InitializeComponent()` setting `AutoScaleMode`, `Name = "EDocumentsPage"`, `Size = new Size(900, 600)`. No other controls (the ElementHost is added in code like ViewSetPage does).

- [ ] **Step 3: Register both files in the csproj (REQUIRED — non-SDK project, see AGENTS.md)**

In `Yakult.Inventory.App/Yakult.Inventory.App.csproj`, find the block containing `<Compile Include="Pages\Reports\...">` entries (same folder group) and insert alphabetically:
```xml
<Compile Include="Pages\Reports\EDocumentsPage.cs">
  <SubType>UserControl</SubType>
</Compile>
<Compile Include="Pages\Reports\EDocumentsPage.Designer.cs">
  <DependentUpon>EDocumentsPage.cs</DependentUpon>
</Compile>
```
Verify no duplicate: run `[regex]::Matches((Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw), 'Compile\s+Include="Pages\\Reports\\EDocumentsPage[^"]*"') | Measure-Object | Select-Object Count` — Expected: Count 2.

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Pages/Reports/EDocumentsPage.cs Yakult.Inventory.App/Pages/Reports/EDocumentsPage.Designer.cs Yakult.Inventory.App/Yakult.Inventory.App.csproj
git commit -m "feat(edocs): add EDocumentsPage WinForms host for WPF dashboard"
```

---

### Task 4: Gatepass data contract + print view-model (mirrors mobile GatepassReport)

**Files:**
- Create: `Yakult.Inventory.App/Wpf/EDocs/Gatepass/Models/GatepassDocument.cs`
- Create: `Yakult.Inventory.App/Wpf/EDocs/Gatepass/ViewModels/GatepassPrintViewModel.cs`
- Reference field list (from mobile `GatepassReport`, TransmittalModels.kt:183-202 — copy every field, C# names in PascalCase):
  `FormType` (enum Gatepass/File/Transmittal, default File), `IsYakultPhilippines`/`IsYakultMarketing` bools, `To`, `From`, `Date`, `ItemCategory` (enum: same members as mobile `GatepassItemCategory` incl. Others + ComputerTableFixedAsset), `ItemCategoryOther` string, `Model` (enum Lx300/Lx310/None), `FixedAssetNumber`, `Quantity`, `Items` (List of `{ Description }`, pad to 12 lines at print), `Others`, `Remarks`, `IssuedBy`, `NotedBy`, `ReceivedByDate`, `ApprovedByDate`.

- [ ] **Step 1: Write GatepassDocument.cs**

Plain POCO, all strings default to `""`, `Items` defaults to empty list, `INotifyPropertyChanged` implemented (copy the pattern from `Wpf/Set/RequisitionForm/ViewModels/RequisitionFormViewModel.cs` property style).

- [ ] **Step 2: Write GatepassPrintViewModel.cs**

Wraps one `GatepassDocument`; exposes `DisplayLines` (exactly 12 strings: item descriptions padded with `""`, mirroring mobile `TEMPLATE_ITEM_COUNT = 12`), `FormTypeIsGatepass/File/Transmittal` bools for checkbox binding, `CopyTopLabel`/`CopyBottomLabel` ("GATEPASS"/"FILE" per selected FormType — mobile default prints two identical copies of the selected type), and `Validate()` returning a string error or null (require To, Date, and at least one item line — same mandatory set the mobile screen enforces before enabling print).

- [ ] **Step 3: Register in csproj (plain .cs = no SubType tag)**

Add inside the existing `Wpf\EDocs\...` group (create the group next to the `Wpf\Set\RequisitionForm\` Compile entries, alphabetical by path):
```xml
<Compile Include="Wpf\EDocs\Gatepass\Models\GatepassDocument.cs" />
<Compile Include="Wpf\EDocs\Gatepass\ViewModels\GatepassPrintViewModel.cs" />
```

- [ ] **Step 4: Commit**

```bash
git add Yakult.Inventory.App/Wpf/EDocs/Gatepass/Models/GatepassDocument.cs Yakult.Inventory.App/Wpf/EDocs/Gatepass/ViewModels/GatepassPrintViewModel.cs Yakult.Inventory.App/Yakult.Inventory.App.csproj
git commit -m "feat(edocs): add gatepass document contract + print view-model"
```

---

### Task 5: Gatepass WPF print view (Legal, 2 copies/page, mobile layout)

**Files:**
- Create: `Yakult.Inventory.App/Wpf/EDocs/Gatepass/Views/GatepassPrintView.xaml`
- Create: `Yakult.Inventory.App/Wpf/EDocs/Gatepass/Views/GatepassPrintView.xaml.cs`
- Reference layout: mobile `drawGatepassForm` (GatepassPrintUtils.kt:117-228) — Legal page 612x1008 units, margin 58, two half-page copies; header: company checkboxes, GATEPASS/TRANSMITTAL/FILE type checkboxes, TO/DATE/FROM lines; body: item-category checkbox column, model LX300/LX310, FIXED ASSET NO., QUANTITY + 12 ruled item lines; footer: OTHERS, REMARKS, ISSUED BY / NOTED BY / RECEIVED BY-DATE / APPROVED BY-DATE.
- XAML rules (AGENTS.md): NO `LetterSpacing`, NO `LineSpacing` (use `LineHeight`), NO `BorderRadius` (use `CornerRadius`), NO `TextColor` (use `Foreground`). After editing, validate XML parses: `[xml](Get-Content "path" -Raw) | Out-Null`.

- [ ] **Step 1: Write GatepassPrintView.xaml**

One `UserControl` (Times New Roman to match company forms) containing a reusable `Grid` copy laid out twice via two `ContentControl`s bound to the same VM (top/bottom halves with a gap — same approach as `RequisitionFormPrintView`, whose two-copy stacking in `RequisitionFormPrintService.cs:26-28` is the in-repo precedent). Sections top to bottom per copy: company checkboxes → type checkboxes → TO/DATE/FROM → category checkboxes + model + asset no → QUANTITY + 12 ruled `ItemsControl` rows bound to `DisplayLines` → OTHERS → REMARKS → 4 sign-off lines. All text via `{Binding}` on `GatepassPrintViewModel`; no hardcoded sample data.

- [ ] **Step 2: Write GatepassPrintView.xaml.cs (code-behind, print only)**

Exposes `Print(string printerName)` using `PrintDialog` + `FixedDocument` pagination identical to the existing print-dialog pattern (`Wpf/Set/RequisitionForm/Dialogs/PrintRequisitionDialog.xaml.cs` — read it first and copy its FixedDocument/PrintDialog flow, swapping only the visual being printed). No business logic in code-behind.

- [ ] **Step 3: Register in csproj (XAML + code-behind pair, copy the RequisitionForm entry shape at csproj:798/2247)**

```xml
<Compile Include="Wpf\EDocs\Gatepass\Views\GatepassPrintView.xaml.cs">
  <DependentUpon>GatepassPrintView.xaml</DependentUpon>
</Compile>
```
```xml
<Page Include="Wpf\EDocs\Gatepass\Views\GatepassPrintView.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
```
(Confirm the exact `<Page>` attribute shape by reading csproj lines 2247-2252 first; match it verbatim.)

- [ ] **Step 4: Validate XAML parses + commit**

Run: `try { [xml](Get-Content "Yakult.Inventory.App\Wpf\EDocs\Gatepass\Views\GatepassPrintView.xaml" -Raw) | Out-Null; Write-Host "VALID XML" } catch { Write-Host "INVALID: $($_.Exception.Message)" }`
Expected: `VALID XML`.
```bash
git add Yakult.Inventory.App/Wpf/EDocs/Gatepass/Views/GatepassPrintView.xaml Yakult.Inventory.App/Wpf/EDocs/Gatepass/Views/GatepassPrintView.xaml.cs Yakult.Inventory.App/Yakult.Inventory.App.csproj
git commit -m "feat(edocs): add gatepass WPF print view (Legal, 2 copies)"
```

---

### Task 6: E-Docs dashboard (modern responsive WPF, 3 tabs reusing existing views)

**Files:**
- Create: `Yakult.Inventory.App/Wpf/EDocs/Views/EDocsDashboardView.xaml`
- Create: `Yakult.Inventory.App/Wpf/EDocs/Views/EDocsDashboardView.xaml.cs`
- Create: `Yakult.Inventory.App/Wpf/EDocs/ViewModels/EDocsDashboardViewModel.cs`

- [ ] **Step 1: Write EDocsDashboardViewModel.cs**

Properties: `ObservableCollection<GatepassDocument> RecentGatepass`, `SelectedTabIndex` int, `ICommand NewGatepassCommand` (creates blank `GatepassDocument`, opens Task 5 view in a print dialog), `ICommand OpenTransmittalCommand` / `ICommand OpenRequisitionCommand` (these two navigate to the existing flows — they do NOT duplicate them: Transmittal reuses `CartridgeTransmittalPrintView`, Requisition reuses `RequisitionFormPrintService.ShowPrintDialog`). No database access in this task (recent-docs persistence is out of scope).

- [ ] **Step 2: Write EDocsDashboardView.xaml (responsive + modern)**

`UserControl` with: header card (title "E-Documents", subtitle "Transmittal • Gatepass • Requisition"), three-card quick-action row (one card per doc type with emoji-free geometric icons drawn as XAML shapes, Segoe UI), and a `TabControl` (Transmittal / Gatepass / Requisition) whose Gatepass tab hosts the Task 5 `GatepassPrintView` and whose Transmittal/Requisition tabs host short explainer cards with buttons bound to the ViewModel commands. Responsiveness: root `VisualStateManager` with two states — `Narrow` (width < 700: quick-action cards stack vertically via single-column Grid, tabs scroll horizontally) and `Wide` (3-column cards); state switch in `SizeChanged` in code-behind (`if (ActualWidth < 700) GoToState(Narrow)`). Palette: white cards, `#F5F7FA` canvas, brand blue `#3498DB`, red `#DC3545` reserved for destructive actions only (matches ReportsForm menu colors). Same XAML property bans + XML validation as Task 5 Step 4.

- [ ] **Step 3: Write EDocsDashboardView.xaml.cs**

Constructor takes no args, sets `DataContext = new EDocsDashboardViewModel()`; `SizeChanged` handler drives the Narrow/Wide states; nothing else.

- [ ] **Step 4: Register all three files in csproj + validate + commit**

Compile entries (no SubType for the VM; DependentUpon for the view code-behind; Page entry for the XAML — same shapes as Task 5 Step 3). XML validation command as in Task 5 Step 4, expected `VALID XML`.
```bash
git add Yakult.Inventory.App/Wpf/EDocs/Views/EDocsDashboardView.xaml Yakult.Inventory.App/Wpf/EDocs/Views/EDocsDashboardView.xaml.cs Yakult.Inventory.App/Wpf/EDocs/ViewModels/EDocsDashboardViewModel.cs Yakult.Inventory.App/Yakult.Inventory.App.csproj
git commit -m "feat(edocs): add responsive E-Docs dashboard (3 tabs)"
```

---

### Task 7: xlsx format conformance check (no code, evidence only)

**Files:** none (read-only verification; findings appended to the Task 8 summary message, not the repo)

- [ ] **Step 1: Re-dump one 2022 sheet and one 2024 sheet, compare signatory label**

Run the Excel-COM dump used in analysis (sheet 1 and sheet "ITD9", rows 24-27 only) and record whether row 24 reads REQUESTED BY or PREPARED BY per era.
Expected: 2022-era sheets say REQUESTED BY, 2024-era say PREPARED BY (desktop already matches the newer wording — no change required; record the result).

- [ ] **Step 2: Confirm every xlsx data cell has a desktop binding**

Map: D7 department → `RequisitionFormViewModel.Department`; J7 date → `.Date`; A12/D12/J12 item start → `Items[].Quantity/Description/Remarks`; row 26/27 names/positions → `PreparedBy/NotedBy/ApprovedBy/ReceivedBy` Name+Position; A1 company check → `IsYakultPhilippines/IsYakultMarketing`.
Expected: full coverage. The only xlsx content with no desktop equivalent is the empty RECEIVED BY name on old sheets (desktop defaults it to `""` — acceptable, record it).

---

### Task 8: Full build + smoke test

**Files:** none (verification only)

- [ ] **Step 1: Confirm clean csproj state (AGENTS.md merge-cascade checks)**

Run:
```powershell
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
$all = [regex]::Matches($csproj, '(?:Compile|Page|EmbeddedResource|None)\s+Include="([^"]+)"')
$dups = $all | Group-Object { $_.Groups[1].Value } | Where-Object { $_.Count -gt 1 }
Write-Host "Duplicates: $($dups.Count)"
$pageEntries = [regex]::Matches($csproj, 'Compile Include="(Pages[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$ghosts = $pageEntries | Where-Object { -not (Test-Path "Yakult.Inventory.App\$_") }
Write-Host "Ghost entries: $($ghosts.Count)"
```
Expected: `Duplicates: 0`, `Ghost entries: 0`.

- [ ] **Step 2: Build the solution**

Run: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe Yakult.Inventory.App.sln /p:Configuration=Debug /verbosity:minimal`
Expected: `Build succeeded.` with 0 errors (warnings acceptable; record warning count).

- [ ] **Step 3: Manual smoke checklist (report pass/fail per line)**

1. Open Reports → E-Documents menu item visible below Cartridge Disposed/Sold.
2. Click it → dashboard loads with 3 cards + 3 tabs, no exception.
3. Gatepass tab → New → fill TO/DATE/one item → print preview shows 2 identical Legal copies.
4. Resize the window narrow → cards stack vertically (responsive state works).
5. Deny `EDocumentsPage` permission for a test user → clicking shows the Unauthorized message.

---

## Self-Review

1. **Spec coverage:** E-Docs menu entry (Tasks 1-2) ✓; mobile transmittal reflected (reuses existing desktop Transmittal view, Task 6 commands) ✓; mobile gatepass reflected (new print view mirroring every mobile field, Tasks 4-5) ✓; xlsx format analyzed + conformance-checked (Task 7, cell map in header) ✓; requisition in edocs (reuses RequisitionForm module, Task 6) ✓; WPF + responsive + modern (Task 6 states/palette) ✓.
2. **Placeholder scan:** no TBD/TODO/"appropriate handling" language; every code step names exact files, bindings, and commands. The one deliberate deferral (PROD permission-seed deploy, recent-docs persistence) is stated as out-of-scope, not a placeholder.
3. **Type consistency:** `GatepassDocument` (Task 4) → consumed by `GatepassPrintViewModel.DisplayLines` (Task 4) → bound by `GatepassPrintView.xaml` (Task 5) → hosted by `EDocsDashboardViewModel/EDocsDashboardView` (Task 6) → hosted by `EDocumentsPage` (Task 3) → opened by `ReportsForm.ShowEDocumentsPage` (Task 2) guarded by `EDocumentsPage` key seeded in Task 1. Names match across all tasks.
