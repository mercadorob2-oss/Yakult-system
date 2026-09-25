# Yakult Inventory System — Claude Code Instructions

## RULE: Always consult this file first when encountering any error

Before investigating an error in code, read this file top-to-bottom. Known issues and their solutions are documented here. This prevents re-diagnosing problems that have already been solved.

## CRITICAL: csproj Compile Include Rule

The main app project (`Yakult.Inventory.App\Yakult.Inventory.App.csproj`) is a **non-SDK-style .NET Framework csproj**. This means **every `.cs` file must be explicitly listed** with a `<Compile Include>` entry — there is no automatic file discovery.

The only exception is the blanket exclusion for `Pages\`:

```xml
<Compile Remove="Pages\**\*.cs" />
```

This removes all `Pages\` files and re-adds them individually. But **all other directories** (including `Forms\`, `Security\`, `Repositories\`, etc.) also require explicit `<Compile Include>` entries — they are NOT auto-included.

### Rule: Every new `.cs` file anywhere in the project must be registered in the csproj

After creating any new `.cs` file, immediately add it to the csproj. Find the existing block of `<Compile Include>` entries for the same folder (they are roughly alphabetical by folder name) and insert the new entry there:

```xml
<Compile Include="Forms\Admin\AdminPortalForm.cs">
  <SubType>Form</SubType>
</Compile>

<Compile Include="Pages\Path\To\NewFile.cs">
  <SubType>UserControl</SubType>
</Compile>
```

Use `SubType`:
- `UserControl` — for classes that extend `UserControl`
- `Form` — for classes that extend `Form`
- No SubType tag — for plain `.cs` files (helpers, DTOs, repositories, etc.)

**Forgetting this causes CS0234 "type or namespace does not exist" errors at build time — the file is invisible to the compiler.**

---

## Project Structure

- `Yakult.Inventory.App\` — main WinForms desktop application (.NET Framework)
- `Request Portal - NET\Inventory.RequestPortal\` — ASP.NET web app (Request Portal)
- `DATABASES\Yakult-DB-Production\` — SQL schema (CREATE TABLE definitions, migration scripts)
- `DATABASES-PROTOTYPE\` — prototype DB schema mirror

## Database Migrations

- Migration scripts live in `DATABASES\Yakult-DB-Production\dbo\Scripts\`
- Naming convention: `Migration_<TableOrFeature>_<Description>.sql`
- Always write migrations as idempotent `ALTER TABLE` scripts with an `IF NOT EXISTS` guard — never modify the `CREATE TABLE` definition files for existing columns
- Example guard pattern:
  ```sql
  IF NOT EXISTS (
      SELECT 1 FROM sys.columns
      WHERE object_id = OBJECT_ID('dbo.[TableName]') AND name = 'ColumnName'
  )
  BEGIN
      ALTER TABLE dbo.[TableName] ADD [ColumnName] BIT NOT NULL DEFAULT (0);
  END
  ```

## Key Table Column Notes

- `dbo.Title` columns: `TitleId`, `Code`, `Description` — **not** `TitleName`
- Join to get a title: `LEFT JOIN dbo.Title t ON e.TitleId = t.TitleId`, then select `t.Code` (preferred display value)

## Session & Permissions

- `AppSession.cs` — static session state set at login
- `IsDeveloper` — set from `dbo.[User].IsDeveloper` column
- `IsSuperAdmin` — set from `dbo.[User].IsSuperAdmin` column; grants access to Account Permissions page and future role management UI
- `IsAdmin` — `IsDeveloper || HasRole("Admin")`
- `IsApprover` — hardcoded position string list in `AppSession.cs`; positions must be added here manually for now
- **Who sees a pending authorization (desktop and web must match):**
  - The rule lives in desktop `CartridgeAuthorizationRepository.GetPendingByScopeAsync` and web `CartridgeAuthorizationWebRepository.GetPendingForApproverAsync`.
  - **Scope:** the approver's company, branch and department.
  - **Manager-title requesters** (`dbo.ApprovalRoleTitle`, role 'Manager') are visible only to themselves (they self-sign). If that manager has **no active user account**, the request falls back to the other approvers in scope, so it can't get stuck.
  - **Everyone else's requests** are visible to all approvers in scope except the requester.
  - Developers / admins see all pending requests.
  - The Approve / Reject actions don't re-check this; the rule only controls what is listed.

## WPF XAML — Properties That Do NOT Exist

WPF XAML is not CSS. Several property names that look reasonable do not exist in `http://schemas.microsoft.com/winfx/2006/xaml/presentation` and will cause a build error.

### `LetterSpacing` — does not exist in WPF

**Wrong:**
```xml
<TextBlock Text="LABEL" LetterSpacing="0.5"/>
```

**Fix:** WPF has no direct letter-spacing property on `TextBlock`. Either omit it (uppercase text already reads distinctly) or use `Typography.Kerning="False"` to disable kerning — but there is no pixel/em spacing control. Just remove it.

Other common non-existent properties to avoid:
| Avoid | WPF equivalent |
|---|---|
| `LetterSpacing` | No direct equivalent — omit |
| `LineSpacing` | Use `LineHeight` on `TextBlock` |
| `BorderRadius` | Use `CornerRadius` on `Border` |
| `TextColor` | Use `Foreground` |
| `Click` on non-Button | Use `MouseLeftButtonDown` or wrap in `Button` |

---

## XML Comments Must NEVER Contain `--`

**This has caused HTTP 500.19 "Configuration file is not well-formed XML" outages more than once in this project** — always on `Web.config` files in `Yakult.Inventory.Api2_remote\`/`Api2_local\`, since those are hand-edited more often than the WPF/csproj XML.

XML's comment syntax (`<!-- ... -->`) forbids a literal `--` anywhere in the comment body — it is only legal as part of the closing `-->` delimiter. A comment like:

```xml
<!-- Does X -- separate from Y -->
```

is **invalid XML**. IIS will fail to parse the whole config file and return `HTTP Error 500.19` with `Config Error: Configuration file is not well-formed XML`, taking the entire site down — not just the section being commented.

**Before adding or editing any XML comment (`Web.config`, `.xaml`, `.csproj`, `.resx`, any `.config`), scan the comment text for a literal `--`** — including em-dash-style usage ("do X -- because Y"), version ranges ("v1--v2"), or double-hyphen used as a dash substitute. Rewrite using a comma, parentheses, or a single word like "since"/"because" instead:

```xml
<!-- WRONG: Does X -- because of Y -->
<!-- RIGHT: Does X, because of Y -->
<!-- RIGHT: Does X (because of Y) -->
```

**After editing any XML/config file, validate it parses before telling the user to deploy it:**
```powershell
try { [xml](Get-Content "path\to\file.xml" -Raw) | Out-Null; Write-Host "VALID XML" }
catch { Write-Host "INVALID: $($_.Exception.Message)" }
```
This check is cheap and must be run on any `Web.config` change before handing it off for deployment — a broken config isn't caught until IIS actually tries to load it, by which point it's already live on the server and the site is down.

---

## Branch Merge Conflicts — csproj and File Content Rules

### The cascade: merging Request-Portal into main breaks the build in layers

When `Request-Portal` is merged into `main`, the csproj ends up in a broken state that reveals itself in layers. Each fix exposes the next problem. The full sequence and its solutions:

---

#### Layer 1 — "An item with the same key has already been added" (MSBuild error, no line number)

**Cause A — Exact-path duplicate `<Compile Include>` entries.** The merge keeps entries from both branches, and some files appear twice at the same path.

**Fix:** Run the dedup script:
```powershell
$path = "Yakult.Inventory.App\Yakult.Inventory.App.csproj"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$seen = @{}
$result = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    '[ \t]*<(?:Compile|Page|None|EmbeddedResource)\s+Include="([^"]+)"[\s\S]*?(?:/>|</(?:Compile|Page|None|EmbeddedResource)>)\r?\n',
    { param($m); $key = $m.Groups[1].Value; if ($seen.ContainsKey($key)) { return '' }; $seen[$key] = $true; return $m.Value }
)
[System.IO.File]::WriteAllText($path, $result, [System.Text.Encoding]::UTF8)
```

**Cause B — Overlapping glob patterns.** A narrower glob (`Pages\Cartridge\**\*.resx`) nested inside a broader one (`Pages\**\*.resx`) causes every matched file to be added twice. Check for this:
```powershell
Select-String -Path "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Pattern 'Include="[^"]*\*\*[^"]*"'
```
If two globs have a parent/child path relationship, remove the narrower one.

---

#### Layer 2 — 300+ CS0246 "type or namespace not found" errors after fixing Layer 1

**Cause:** `Request-Portal` introduced a `<Compile Remove="Pages\**\*.cs" />` block in a second `<ItemGroup>`. After the merge, all the `main`-branch Pages entries land in the first ItemGroup (before the Remove), so the Remove wipes them all out. Only files explicitly listed after the Remove survive.

**Fix:** Delete the `<Compile Remove="Pages\**\*.cs" />` and `<EmbeddedResource Remove="Pages\**\*.resx" />` lines entirely. This csproj is non-SDK-style — there is no auto-globbing, so these Remove elements serve no purpose and only cause harm.

**Warning:** `Pages\Dtos.cs` (which defines `UserAccountDto`, `ItemLookupDto`, `ItemCategoryDto`, `ConditionDto`, and many others) lives in the `Pages\` namespace. If it ends up before the Remove it gets wiped and causes a cascade of 300+ CS0246 errors across `UserRepository.cs`, `AddExternalAndBorrowDialog.cs`, `MarkAsResolutionDialog.cs`, etc. After deleting the Remove, confirm `Pages\Dtos.cs` still has an entry in the csproj.

---

#### Layer 3 — ~108 CS2001 "source file could not be found" errors

**Cause:** The merge brought flat-path ghost entries like `Pages\ViewBrandNewCartridgesPage.cs` that don't exist on disk — the real files are at subdirectory paths like `Pages\Cartridge\ViewBrandNewCartridgesPage.cs`. These are stale entries from an earlier state of `Request-Portal`.

**Fix:** Audit and remove all ghost entries:
```powershell
$path = "Yakult.Inventory.App\Yakult.Inventory.App.csproj"
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$entries = [regex]::Matches($content, 'Compile Include="(Pages[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$ghosts = $entries | Where-Object { -not (Test-Path "Yakult.Inventory.App\$_") }
foreach ($ghost in $ghosts) {
    $escaped = [regex]::Escape($ghost)
    $content = [regex]::Replace($content, "[ \t]*<Compile Include=""$escaped"" />\r?\n", "")
    $content = [regex]::Replace($content, "[ \t]*<Compile Include=""$escaped"">[\s\S]*?</Compile>\r?\n", "")
}
[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
Write-Host "Removed $($ghosts.Count) ghost entries."
```

---

#### Layer 4 — CS0246 "ambiguous reference between two namespaces" for the same type

**Cause:** The same `.cs` file exists at two disk paths (e.g. `Pages\ViewCartridgeSetPage.cs` AND `Pages\Cartridge\ViewCartridgeSetPage.cs`) and both are registered in the csproj. The ghost audit in Layer 3 misses these because both files genuinely exist on disk. The compiler sees two classes with the same name in different namespaces and can't resolve which one to use.

**Symptoms:** `CS0104 'Foo' is an ambiguous reference between 'Yakult.Inventory.App.Pages.Foo' and 'Yakult.Inventory.App.Pages.Cartridge.Foo'`. Can also cascade into CS0246 "type not found" errors in other files that depend on the ambiguous type.

**Fix:** Determine the canonical subdirectory path (e.g. `Pages\Cartridge\ViewCartridgeSetPage.cs`) and remove the flat-path entry from the csproj. Do not delete the file from disk.

**Known cases:** `Pages\ViewCartridgeSetPage.cs` (canonical: `Pages\Cartridge\`).

**How to find all remaining cases:**
```powershell
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
$entries = [regex]::Matches($csproj, 'Compile Include="(Pages[^"]+\.cs)"') | ForEach-Object { $_.Groups[1].Value }
$entries | Group-Object { Split-Path $_ -Leaf } | Where-Object { $_.Count -gt 1 } | ForEach-Object {
    Write-Host "AMBIGUOUS: $($_.Name)"; $_.Group | ForEach-Object { Write-Host "  $_" }
}
```

---

#### Layer 5 — CS0102/CS0111 "already contains a definition" (with a specific type name)

**Cause:** A file exists at TWO different disk paths (e.g. `Pages\VendorCartridgeRefillPage.cs` AND `Pages\Cartridge\VendorCartridgeRefillPage.cs`), and both paths are registered in the csproj. The ghost audit in Layer 3 misses these because both files exist on disk.

**Fix:** Find which path is the correct one (the subdirectory path), then remove the flat-path `<Compile Include>` entries for both the `.cs` and `.Designer.cs`. Do not delete the files from disk — just remove the wrong-path csproj entries.

**Known cases:** `VendorCartridgeRefillPage.cs` / `VendorCartridgeRefillPage.Designer.cs` — the correct path is `Pages\Cartridge\`.

---

#### Layer 6 — "Two output file names resolved to the same output path: ...Pages.Branch.Foo.resources"

**Cause:** A specific `<EmbeddedResource Include="Pages\Branch\Foo.resx">` entry AND the wildcard `<EmbeddedResource Include="Pages\**\*.resx" />` both match the same file, so the `.resources` output is generated twice under the same name.

**Symptoms:** MSBuild error (no code): `Two output file names resolved to the same output path: "obj\Debug\Yakult.Inventory.App.Pages.Branch.Foo.resources"`.

**Fix:** Remove the specific `<EmbeddedResource>` entries for any `.resx` files under `Pages\` — the wildcard already covers them. The `<DependentUpon>` metadata on those entries is cosmetic (controls Solution Explorer nesting) and safe to drop.

**Known affected files (all removed in favour of the wildcard):**
- `Pages\User\LoginPage.resx`
- `Pages\User\MainForm.resx`
- `Pages\User\RegisterPage.resx`
- `Pages\Invoice\ViewInvoiceDetailPage.resx`
- `Pages\Request\ViewRequestsPage.resx`
- `Pages\Request\EditRequestDialog.resx`
- `Pages\Branch\EditBranchDialog.resx`

**How to detect:** After a merge, run:
```powershell
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
[regex]::Matches($csproj, 'EmbeddedResource Include="(Pages\\[^*][^"]+\.resx)"') |
    ForEach-Object { $_.Groups[1].Value }
```
Any result that falls under `Pages\` (not `Pages\InvoiceImportDialog.resx` which is explicitly excluded from the wildcard) is a duplicate — remove it.

---

#### Layer 7 — CS0102/CS0263 duplicate definitions from `.cs` + `.xaml.cs` for the same class

**Cause:** Some classes have two implementations on disk — an old WinForms `.cs` and a newer WPF `.xaml.cs`. If both get registered, the compiler sees two partial class definitions with different base classes (CS0263) and duplicate members (CS0102/CS0111). This happens when a bulk "add all missing files" script adds the old `.cs` without knowing a `.xaml.cs` already covers it.

**Known cases:** `Pages\Request\BatchAddRequestDialog.cs`, `Pages\Item\BatchAddItemDialog.cs`

**Exception:** `Pages\Set\ViewSetDetailPage.cs` looks like a conflict but is NOT — it only contains helper classes (`OrgSetItem`, `EmployeeItem`, `ReceivedByItem`), not the `ViewSetDetailPage` partial class itself. It must stay registered alongside `ViewSetDetailPage.xaml.cs`.

**Rule:** Before removing a flagged `.cs`, open it and confirm it actually declares `partial class Foo`. If it only contains helper types, keep it — the detection script produces a false positive on it.

**Detection script (run before committing any new csproj entries):**
```powershell
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
$entries = [regex]::Matches($csproj, 'Compile Include="([^"]+\.cs)"') | ForEach-Object { $_.Groups[1].Value }
$conflicts = $entries | Where-Object {
    $_ -notmatch '\.xaml\.cs$' -and $_ -notmatch '\.Designer\.cs$' -and
    ($entries -contains ($_ -replace '\.cs$', '.xaml.cs'))
}
Write-Host "Conflicts: $($conflicts.Count)"; $conflicts
```
Should always print 0.

---

#### Final verification after all layers are fixed

Run this to confirm a clean state before committing:
```powershell
# 1. No duplicates
$csproj = Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw
$all = [regex]::Matches($csproj, '(?:Compile|Page|EmbeddedResource|None)\s+Include="([^"]+)"')
$dups = $all | Group-Object { $_.Groups[1].Value } | Where-Object { $_.Count -gt 1 }
Write-Host "Duplicates: $($dups.Count)"

# 2. No ghost Pages entries
$pageEntries = [regex]::Matches($csproj, 'Compile Include="(Pages[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$ghosts = $pageEntries | Where-Object { -not (Test-Path "Yakult.Inventory.App\$_") }
Write-Host "Ghost entries: $($ghosts.Count)"
```
Both should print 0.

---

### File content reversion after local branch falls behind remote

**What happened:** A commit that removes a UI element (e.g. `8c9f035 SYSTEM: Removed Register Form`) is merged into `origin/main` via a PR on GitHub. But the local `main` branch was never pulled — it was behind `origin/main` by several merged PRs. The local checkout of `main` still had the old file content (with the Register button), so the app appeared unaffected by the removal.

**Cause:** The local branch diverged from `origin/main`. `git log --oneline origin/main..HEAD` shows nothing (local is behind, not ahead), but `git log --oneline -3` shows a different commit history than `git log --oneline -3 origin/main`. The file on disk matches the local HEAD, not the remote.

**How to detect:**
```powershell
git log --oneline -5
git log --oneline -5 origin/main
```
If the two outputs differ, the local branch is out of sync with remote.

**Fix:** Pull from remote, resolving any conflicts:
```powershell
git pull origin main
```
For csproj conflicts during pull: keep the local fixed version (`git checkout --ours`). For CLAUDE.md conflicts: keep whichever version is more up to date.

**Prevention:** Always run `git pull origin main` after switching to `main`, before doing any work or build checks.

---

## Key Patterns

### Adding a new page under Account Management menu
1. Create the `UserControl` class under `Pages\Admin\Account-Management\`
2. Add `<Compile Include>` entry in the csproj (see rule above)
3. Add a sub-menu button in `MainForm.cs` inside `accountManagementPanel` (buttons are added in **reverse visual order** — last added appears at top)
4. Add a `ShowXxxPage()` method in `MainForm.cs` with the appropriate access guard

---

## WPF Overlay Crash on Close — "App Closes Silently When Tour Ends"

**Where this lives:** `Yakult.Inventory.App\Wpf\RequestPortal\WalkThrough\WpfPortalTourWindow.xaml.cs` and `WpfPortalTourService.cs`

---

### Symptom

Clicking the Close or Skip button on any tour step — especially the final step, or any step with a popup dialog open — silently closes the entire `RequesterPortalForm`. No exception dialog appears, no `FormClosing` event fires, `Debug.WriteLine` stops mid-method.

---

### Exact crash chain (step by step)

1. Tour ends → code calls `_window.Close()` or `_window.Hide()` on the WPF overlay.
2. `Close()` destroys the HWND. `Hide()` sets `WS_VISIBLE = 0`. Either way, Win32 must re-activate another window.
3. Win32 posts `WM_ACTIVATE` to `RequesterPortalForm` (the overlay's Win32 owner).
4. WinForms processes `WM_ACTIVATE` and runs its focus-restoration logic. It finds the last control that had keyboard focus — which was an `ElementHost` inside `RequesterPortalForm`.
5. `ElementHost` focus-restoration calls into WPF and tries to give keyboard focus back to the last WPF element that had it — which was a button or card **inside the overlay window that was just destroyed**.
6. WPF throws a dispatcher exception (the target element's visual tree no longer exists).
7. **`Application.Current` is always `null` in this app** — WPF is hosted inside a WinForms process with no `System.Windows.Application` object. Therefore `Application.DispatcherUnhandledException` is never subscribed and never fires.
8. The exception reaches `AppDomain.UnhandledException` with `IsTerminating = true`. The CLR terminates the process immediately. `FormClosing` and `FormClosed` are never called.

**Why `FormClosing` never fires:** The process is killed by the CLR at step 8 — it is not a normal WinForms shutdown. There is no code path that runs between the exception and the process exit.

**Why it only broke on the last step:** Earlier steps called `_window.Hide()` to suspend the tour (Alt+Tab), but the overlay was immediately re-shown. On the final step the window was hidden and then `_window = null` was set, so nothing ever restored the HWND — the focus-restoration exception fired with no living window to receive focus.

---

### What was tried and why it did NOT work

| Attempt | Why it failed |
|---|---|
| `Keyboard.ClearFocus()` before `Hide()` | WPF clears its own focus tracking, but WinForms still has a stale reference to the last-focused `ElementHost` child. WinForms focus-restoration fires independently. |
| `Opacity = 0` + deferred `Close()` at `DispatcherPriority.ApplicationIdle` | The deferred `Close()` still destroys the HWND. The WM_ACTIVATE still fires. The exception still throws — just later, after the lambda returns, outside all try/catch. |
| Subscribing `Dispatcher.CurrentDispatcher.UnhandledException` + unsubscribing in `CloseTourWindow()` | The handlers were unsubscribed before the deferred Close() fired. The WM_ACTIVATE that followed the HWND destruction was processed after the handlers were already gone. |
| Keeping handlers alive until after deferred Close() via `afterCloseCleanup` callback | Worked for the WM_ACTIVATE from Close(), but added fragile timing logic and deferred lambdas. Replaced by the architectural fix below. |

---

### The fix — Singleton overlay, never destroy the HWND during normal operation

**Core principle:** If the HWND is never destroyed, Win32 never posts WM_ACTIVATE, WinForms never runs focus-restoration into a dead window, and the exception never occurs.

#### Lifecycle (current implementation)

```
App opens RequesterPortalForm
    ↓
First StartTour() call
    → EnsureOverlayCreated() creates WpfPortalTourWindow once, sets Win32 owner,
      calls Show() to materialise the HWND, immediately calls HideTour() (Opacity=0)
    → overlay HWND lives for the entire app session

Tour starts  → ShowTour()  (Opacity=1, IsHitTestVisible=true)
Tour ends    → HideTour()  (Opacity=0, IsHitTestVisible=false) — HWND stays alive
Tour starts  → ShowTour()  (same window, reused)
Tour ends    → HideTour()

App closes (FormClosed → Dispose())
    → _window.Close() is safe here — the process is already shutting down
```

#### HideTour() — the key method (in WpfPortalTourWindow.xaml.cs)

```csharp
public void HideTour()
{
    Keyboard.ClearFocus();
    Opacity          = 0.0;
    IsHitTestVisible = false;
    // DO NOT call Hide() or Close() — both cause WM_ACTIVATE → focus restoration crash.
    // Opacity=0 keeps WS_VISIBLE set so Win32 never posts WM_ACTIVATE.
}
```

#### ShowTour() — restore after hide

```csharp
public void ShowTour()
{
    if (!IsVisible) Show();   // only on very first call; window stays shown after that
    Opacity          = 1.0;
    IsHitTestVisible = true;
}
```

#### EnsureOverlayCreated() — in WpfPortalTourService.cs

```csharp
private bool EnsureOverlayCreated()
{
    if (_window != null && !_window.IsClosed()) return true;

    // Resolve Win32 owner — overlay MUST have one or it appears in the taskbar.
    if (_hostForm == null || _hostForm.IsDisposed)
        _hostForm = ResolveHostForm();   // three-tier fallback (see ResolveHostForm())
    if (_hostForm == null) return false; // abort StartTour() if no owner found

    _window = new WpfPortalTourWindow();
    _window.ShowInTaskbar = false;
    // Set owner BEFORE Show() — must be in the right owner chain from the start.
    var interop = new WindowInteropHelper(_window);
    interop.EnsureHandle();
    interop.Owner = _hostForm.Handle;

    _window.Show();
    // Set OnExplicitShutdown AFTER Show() — WPF may create Application.Current on
    // first Show() and default ShutdownMode to OnMainWindowClose, which would close
    // the entire app when the overlay window closes.
    if (System.Windows.Application.Current != null)
        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

    _window.HideTour();   // immediately invisible; shown by ShowOverlay() when a tour starts
    return true;
}
```

#### CloseTourWindow() — teardown order matters

```csharp
private void CloseTourWindow()
{
    _active = false;
    _suppressHostEvents = true;
    CancelPendingSuppressReset();

    if (_window != null && !_window.IsClosed())
    {
        // 1. Clear delegates FIRST so stale button clicks after teardown are no-ops.
        _window.OnNextClicked = null;
        _window.OnPrevClicked = null;
        _window.OnSkipClicked = null;
        // 2. Hide overlay BEFORE closing the dialog (see "dialog ordering" below).
        _window.HideTour();
    }

    CloseTourDialog();   // detaches FormClosing, calls ExitTourMode(), disposes dialog
    CleanupDummyData();

    _steps        = null;   // release all OnEnter/OnLeave/TargetWpf lambda closures
    _currentIndex = 0;
    _suspended          = false;
    _suppressHostEvents = false;
    // _window is NOT nulled — reused by the next StartTour() call.
}
```

---

### Why hiding the overlay BEFORE closing the dialog matters

When `CloseTourDialog()` destroys the WinForms dialog, Win32 must activate a new window. If the overlay is still visible at that moment, Win32 picks it (it is the topmost visible window) and sends WM_ACTIVATE to it. Then HideTour() deactivates it and Win32 sends WM_ACTIVATE again to RequesterPortalForm — a **double WM_ACTIVATE storm** through ElementHost interop that corrupts WPF focus state for the rest of the process lifetime (all subsequent tours in the same session would also break).

With the overlay hidden first, the dialog's WM_DESTROY activates RequesterPortalForm directly — one clean, predictable activation.

---

### Why delegate properties instead of events

```csharp
// WRONG — events accumulate subscriptions on reuse:
public event Action NextClicked;
_window.NextClicked += OnNext;   // adds a new handler every StartTour()

// CORRECT — delegate properties overwrite:
public Action OnNextClicked { get; set; }
_window.OnNextClicked = OnNext;  // replaces previous handler; safe to reuse
```

If `event` syntax is used, each call to `StartTour()` adds another `OnNext` handler. By the second tour, every button click fires `OnNext` twice. By the tenth, ten times. Clearing with `_window.OnNextClicked = null` in `CloseTourWindow()` fully detaches — not possible with `event` syntax from outside the class.

---

### Win32 owner resolution — ResolveHostForm() three tiers

The overlay MUST have a Win32 owner. Without one, `ShowInTaskbar = false` is ignored by Windows (top-level ownerless windows always get a taskbar button).

```
Tier 1: _notifBellHost.FindForm()          — standard path, works in all normal flows
Tier 2: Application.OpenForms scan         — searches by GetType().Name == "RequesterPortalForm"
         (type name string avoids circular assembly reference)
Tier 3: Form.ActiveForm                    — last resort; logs a warning that it may be wrong
If all fail: log warning, return false → StartTour() aborts cleanly
```

---

### Permanent facts about this WPF/WinForms interop app

- **`Application.Current` is always `null`** — no `System.Windows.Application` was ever created; WPF runs purely via `ElementHost`.
- **`Application.DispatcherUnhandledException` never fires** — if you need a global WPF exception handler use `Dispatcher.CurrentDispatcher.UnhandledException`.
- **`Form.Activated` / `Form.Deactivate` on `RequesterPortalForm` fire only on Alt+Tab (OS-level focus change)**, NOT when WPF child windows within the same process gain or lose focus. During tours, `[Portal] Activated` and `[Portal] Deactivated` will never appear in the debug log from WPF overlay activation.
- **`WindowInteropHelper.Owner` must be set before `Show()`** — setting it after Show() has no effect.
- **Never own a modal WPF dialog by `Form.ActiveForm` from a page hosted in an `ElementHost`.** `ActiveForm` is null whenever the app isn't the foreground window at that instant (for example right after a MessageBox closes). The dialog then opens ownerless, **behind** the form it disables, and the whole app looks frozen (even minimize and close stop working). Resolve the hosting form instead: `PresentationSource.FromVisual(this)` → `HwndSource.Handle` → `Control.FromChildHandle(...).TopLevelControl`. Then pass that handle, e.g. `RequisitionFormPrintService.ShowPrintDialog(vm, hostFormHandle)` (see `MixedRequestExchangeView.GetHostFormHandle`).
- **`ShutdownMode` must be set to `OnExplicitShutdown` after every `Show()` call** — WPF resets it to `OnMainWindowClose` when it first creates `Application.Current`.

## Cartridge Lines in Mixed Portal Submissions

A portal submission that mixes cartridges with Ink/Toner/Printhead gets `WorkflowType = 'RequestSetManagement'` for every line, so its cartridge lines never reach the Cartridge Management queue. That queue filters on `WorkflowType`, **not** on whether a `dbo.CartridgeRequestModel` row exists. They are still **cartridge exchanges**:

- Every portal cartridge line (cartridge-only **and** mixed) gets a `dbo.CartridgeRequestModel` row holding its model and the declared `GoodEmptyQty` / `DamagedEmptyQty` (`InsertCartridgeRequestModel` in both portals' `RequesterPortalService`). No extra columns or migration are needed. Readers of that table were checked: the Cartridge queues and fulfilled history select on `WorkflowType` / Set counts, not on this table.
- **Desktop approver screens show every line of a mixed request.** The `RequestedModels` list in `CartridgeAuthorizationRepository` (`GetAllPendingAsync`, `GetPendingByScopeAsync`, `GetHistoryByEmployeeAsync`, `GetSignedByUserAsync`, `GetDetailByIdAsync`) uses the full line list saved on `dbo.CartridgeAuthorization.RequestedModels` at submit time for Request & Set Management submissions. That is the same list the web approval pages and Authorization Monitor show. Cartridge-only submissions still build it from `CartridgeRequestModel`. `SupervisorApprovalPage` hides the Good / Damaged figures for lines with no empties (Ink / Toner / Printhead).
- Fulfill them with `RequestRepository.FulfillCartridgeLine(reqId, brandNew, refilled, ...)`, which calls `CartridgeManagementRepository.IssueMixedCartridgeLine`: FIFO Brand New/Refilled units, a `CartridgeMovement` per unit, and returned empties through the shared `RecordReturnedEmpties` helper (also used by `FulfillCartridgeExchangeByCondition`). The issued qty goes to `Request.IssuedQty`, and any shortfall stays pending in Request & Set Management (no `UnfulfilledCartridgeExchange` row).
- `RequestRepository.FulfillRequest` **throws** for these lines (`GetMixedCartridgeLineInfo(reqId).IsExchangeLine`). Any new screen that issues Request lines must route cartridge lines to `FulfillCartridgeLine`. Current callers: Mixed Request Exchange, and Unfulfilled / Partially Fulfilled Requests (the shared `FulfillRequestDialog`).
- The Unfulfilled / Partially Fulfilled Requests queue (`BuildFulfillmentTrackedRequestsCte`) is scoped to Ink/Toner/Printhead **plus** these mixed cartridge lines. Cartridge-only submissions stay excluded.
- **Web portal mirror:** `RequestFulfillmentRepository` (same scope; `FulfillRequestAsync` refuses these lines), `CartridgeExchangeRepository.GetMixedCartridgeLineInfoAsync` / `IssueMixedCartridgeLineAsync` / `RecordReturnedEmptiesAsync` (shared with `FulfillCartridgeExchangeByConditionAsync`), and `RequestFulfillmentController.Fulfill`, whose form shows Brand New / Refilled inputs and the empties preview for these lines. Keep desktop and web in sync when changing either.

### Approval gate on fulfillment (desktop and web)

Portal requests can only be issued once their submission's `dbo.CartridgeAuthorization` is **Approved or Used** (`Used` means approved, then consumed; nothing sets it today, but treat it as approved). The approval never changes when lines are fulfilled, so a request that is Unfulfilled or Partially Fulfilled stays approved and stays issuable.
- **Unfulfilled / Partially Fulfilled Requests** (`BuildFulfillmentTrackedRequestsCte` on desktop, `FulfillmentTrackedRequestsCte` on web) list a portal request only when it is approved **and** in a Set. IT-Assisted submissions are grouped into a Set at submit time; New Request submissions are grouped when IT first fulfills them on Mixed Request Exchange. So a submission is on exactly one page at a time, and Pending / Rejected ones appear on neither.
- **Mixed Request Exchange** (`GetApprovedMixedRequestLines`) lists approved submissions that are not in a Set yet.
- **Issue methods refuse unapproved requests:** `RequestRepository.EnsurePortalRequestApproved` (desktop `FulfillRequest` / `FulfillCartridgeLine`) and `RequestFulfillmentRepository.EnsurePortalRequestApprovedAsync` (web `FulfillRequestAsync` / `IssueMixedCartridgeLineAsync`).
- **Exempt:** admin-created requests (no `SubmissionSessionId`) and old portal rows with no authorization row at all, so they are not stranded.
- **Web limitation:** the web has no Mixed Request Exchange page, so a New Request submission's first fulfill must be done in the desktop app.

### Consumable stock deduction (Ink / Toner / Printhead)

Consumable `Item.StockOnHand` is deducted **when units are issued**, and nowhere else. Request creation and Set grouping never touch stock (see the "Previously … INCORRECT" comments in `RequestRepository.AddRequest`).
- **Fulfill:** `RequestRepository.FulfillRequest` (desktop) and `RequestFulfillmentRepository.FulfillRequestAsync` (web) deduct exactly the IssuedQty increase from the line's `Item`, in the same transaction. They refuse (THROW 50012) instead of going negative.
- **Deploy:** `SetRepository.DispatchSetAsync` sets IssuedQty = Quantity for every line, so it first deducts the still-unissued remainder of consumable lines (floored at 0). It does this only on the call that actually dispatches, so a repeat Deploy never deducts twice.
- **Cartridges** are deducted per unit by the cartridge paths (`DecreaseItemStock`).
- **IT custody items are not stock.** Returned empties of non-refillable models are kept as an Item named "&lt;model&gt; - Returned Empty" with `Remarks LIKE '%IT custody%'` and no RefillStatus. That looks like Brand New stock, so every issuable-stock query must add `AND ISNULL(i.Remarks, '') NOT LIKE '%IT custody%'`. Already done in `CartridgeManagementRepository` (GetAvailableIssuableStock*, GetIssuableItemIds*, GetIssuableCartridges), the web `CartridgeExchangeRepository` / `CartridgeFulfillmentRepository`, and the Cartridge Models page counts (`CartridgeModelRepository`). The dispose/sell workflow finds those items by the same marker.
- **The `SetRepository` "ONLY place where stock should be deducted" comment** applies to item **upgrades**, not normal issuing.
- **Before 2026-09-25** none of this existed, so older consumable stock figures were never reduced by issued requests.

### Zero-stock fulfillment on Mixed Request Exchange (mirrors Cartridge Exchange)

A submission stays on Mixed Request Exchange until IT clicks Fulfill. Fulfill always works, even with every quantity at 0 (no stock), exactly like the Cartridge Exchange. The panel shows a live **Fulfillment Status** (`MixedGroupViewModel.ResultStatus`), and each line's badge shows its status after this fulfill; the left list shows the current status (`CurrentStatus`). On Fulfill the submission is grouped into its Set and ends up:
- **Unfulfilled** (nothing issued): on Unfulfilled Requests
- **Partially Fulfilled**: on Partially Fulfilled Requests
- **Fulfilled**: done

Lines that got nothing are sent the Cartridge Exchange's "Request Unfulfilled" notification (`RequestRepository.NotifyUnfulfilled`). The printable requisition form is offered after every fulfill, like the Cartridge transmittal.

## Portal Media Import (yt-dlp) Maintenance

- **What:** The Learning Content admin studio's "Import from URL" option downloads YouTube videos locally via `Yakult.SystemsPortal\tools\yt-dlp.exe` (no ffmpeg needed; formats restricted to combined MP4). Direct .mp4/.webm URLs are fetched with HttpClient instead.
- **yt-dlp breaks periodically** when YouTube changes its page structure. If imports start failing with sanitized yt-dlp errors, update the binary from the official release: `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`. The exe is git-ignored (too large) but ships automatically on publish via the csproj `Content` entry (guarded by `Exists(...)`), so replacing it locally and redeploying is all that is needed.
- **Server override:** set `MediaImport:YtDlpPath` in `appsettings.Local.json` to use a different binary location (e.g. if the server cannot reach the publish output).
- **Limits:** 500 MB max (enforced before and after download), 20-minute timeout, cancellation kills the yt-dlp process tree. Errors are sanitized (URLs and ANSI codes stripped) before being shown to the admin.

## Parent Tag Feature (Invoice Items)

Free-text grouping for invoice line items (e.g. "Cisco", or a whole project title) — an independent, orthogonal dimension from Sub-Type Group (Contract/Subscription/License/Services), with no financial semantics of its own (no VAT/WHT/Discount overrides, no reference code, no date range). An item can belong to both a Sub-Type Group and a Parent Tag Group at once. Confirmed display hierarchy everywhere it appears: **Sub-Type Group (outer) → Parent Tag (middle) → Category (inner) → item rows**.

### Database

- `dbo.SetItemParentTagGroup` (`ParentTagGroupId` PK, `SetId` FK, `Label` nvarchar(200), audit columns) — one row per group, mirrors `dbo.SetItemSubTypeGroup` but with no override columns.
- `dbo.SetItem.ParentTagGroupId` — FK column, sibling to the existing `GroupId` (Sub-Type).
- `dbo.vw_SetItemParentTagGroups` — aggregates `ItemCount`, `SUM(Amount) AS Subtotal` per group. No override precedence logic (unlike `vw_SetItemSubTypeGroups`) since there are no overrides.
- Migration scripts: `Migration_SetItemParentTagGroup_CreateTable.sql`, `Migration_SetItem_AddParentTagGroupId.sql`, `CreateView_vw_SetItemParentTagGroups.sql` in `DATABASES\Yakult-DB-Production\dbo\Scripts\`.

### Code architecture (mirrors the Sub-Type Group pattern throughout)

- `Repositories\SetItemParentTagGroupHelper.cs` — shared find-or-create logic (`FindOrCreateGroupId`), keyed on `SetId + Label` (trimmed). Used by both `InvoiceRepository` and `ServiceSetRepository` so every SetItem-inserting code path resolves the same group row.
- `InvoiceRepository`: `AddInvoiceItemsAsync` takes an optional `parentTagLabel` param; `ConvertItemsToParentTagGroupAsync`/`RemoveParentTagGroupFromInvoice` mirror the Sub-Type equivalents; `GetInvoiceItemsAsync`/`GetRequestItemsAsInvoiceItemsAsync` select `ParentTagGroupId`/`ParentTag` (joined from `SetItemParentTagGroup`) and `Category` (joined from `ItemCategory`).
- `ServiceSetRepository` — `FindOrCreateParentTagGroup` wrapper alongside the existing `FindOrCreateSubTypeGroup`, called at both `SetItem` insert sites.
- `Pages\Invoice\ConvertToParentTagGroupDialog.xaml(.cs)` — after-the-fact retagging dialog, modeled on `ConvertToSubTypeGroupDialog` but with a free-text/autocomplete `ComboBox` instead of a fixed-enum one.
- `Pages\Item\InvoiceBuilderDialog` — each `BuilderGroup` has a free-text `ParentTagLabel` field (not a fixed catalog like `SubTypeOptions`), flowing through `InvoiceCsvImportDialog.InvoiceCsvRow.ParentTagLabel` → `RunImport` → `ServiceSetItemDto.ParentTagLabel`.

### WPF grid display gotcha — `GroupSumConverter` must recurse for nested grouping

`Pages\Invoice\ViewInvoiceDetailPage.xaml.cs` groups the Invoice Items `DataGrid` three levels deep (`SubTypeGroupKeyConverter` → `ParentTagGroupKeyConverter` → `CategoryGroupKeyConverter`, added to `GroupDescriptions` in that order). Any converter that sums a column across a group's member rows (`GroupSumConverter`, used for the Parent Tag banner's rolled-up Qty) **must recurse through nested `CollectionViewGroup` children** — at any level that isn't the innermost, `CollectionViewGroup.Items` contains child `CollectionViewGroup` objects (the next grouping level down), not `DataRowView` leaf rows directly. Checking only for `DataRowView` and skipping everything else silently sums to 0 with no error. Fixed by a recursive `AddSum(IEnumerable items, ...)` helper that unwraps nested `CollectionViewGroup`s down to the leaves.

### RDLC report gotcha — a parameter referenced in an expression must be explicitly declared

Reports (`InvoiceReport.rdl`/`_Wide`/`_Compact`, `RenewalsReport.rdlc`/`_Wide`/`_Compact`/`_Normal`, `RenewalsGroupedReport.rdlc`) each got a new `ParentTagGroup` Tablix grouping level (nested inside `SubTypeGroup`, wrapping `Details`) and a banner row gated by `=Parameters!ShowParentTagGroups.Value = false OR ...`. **Referencing `Parameters!X.Value` in an expression is not enough** — the parameter must also have an explicit `<ReportParameter Name="X">...</ReportParameter>` block in the RDL's `<ReportParameters>` section (mirroring the existing `ShowSubTypeGroups` one), or the report throws `Microsoft.Reporting.WinForms.LocalProcessingException` ("An error occurred during local report processing") at render time the moment that parameter is actually passed a value — not at compile/design time, so it isn't caught until someone actually checks the corresponding box in the column picker. `Helpers\ReportLauncher.cs` also needed `ParentTagGroupSelectSql`/`...JoinBySetItemSql`/`...JoinBySetItemKeySql` constants (mirroring the Sub-Type ones) wired into all 7 dataset-building call sites, and `"ShowParentTagGroups"` added to `_summaryRowParamNames` so it's excluded from the Wide/Compact/Normal column-count math. `RenewalsGroupedReport_Wide/_Compact/_Normal.rdlc` are confirmed dead files (never referenced by `ReportLauncher.cs`) — don't bother editing them for future report-level changes either.

### Invoice Sub Groups page — InvoiceBuilderDialog bypasses the staging table

`InvoiceBuilderDialog`'s save path (`InvoiceCsvImportDialog.RunImport` → `ServiceSetRepository`) writes Sub-Type/Parent Tag groups directly to `dbo.SetItemSubTypeGroup`/`dbo.SetItem`/`dbo.[Set]`, and **never** touches `dbo.InvoicePreparation`/`dbo.InvoicePreparationItem` — the staging tables `Wpf\InvoicePreparation\ViewModels\InvoicePreparationListViewModel.cs` (the "Invoice Sub Groups" page, both Completed and Not Completed tabs) reads from exclusively. Fixed by `InvoicePreparationRepository.RegisterCompletedGroupsFromSet(setId, createdBy)` — called best-effort right after `RunImport`'s transaction commits, backfilling one `InvoicePreparation` (`Status='Completed'`, `GeneratedSetId` set) + matching `InvoicePreparationItem` rows per Sub-Type group the invoice just created, mirroring `RemoveGroupFromInvoice`'s reverse round-trip. Only covers Sub-Type groups (matching what that page displays) — Parent Tag groups have no equivalent staging concept.

### InvoiceBuilderDialog — existing-item picker (per-row, not per-table)

Each line in `InvoiceBuilderDialog`'s grid has its own "Existing?" checkbox (leftmost column, styled with a persistent light-orange cell background as a visual affordance). Checking it opens `Pages\Software\SoftwareItemPickerDialog` (reused as-is — a multi-select, searchable existing-item picker already used by `SoftwareServiceSetDialog`) for **that row only**; picking an item populates it and locks its new-item-only fields (`LinesGrid_BeginningEdit` cancels edits on those columns when `BuilderLine.IsExistingItem` is true), and the whole row turns orange (`DataGrid.RowStyle` trigger) to confirm. "+ Add line"/"+ 5 lines" always add plain blank rows and never open the picker themselves — marking a specific row as an existing item is a deliberate, separate, per-row action.

Two non-obvious fixes baked into this:
- **Checkbox visual-commit race**: opening the picker (`ShowDialog`, a blocking modal call) synchronously from inside the same click that commits the checkbox's bound value can visually show the checkbox still unchecked until the modal closes, because the DataGrid never gets a render pass in between. Fixed by deferring the actual picker-opening call one dispatcher cycle via `Dispatcher.BeginInvoke(..., DispatcherPriority.Background)`.
- **Multi-select in the picker**: since `SoftwareItemPickerDialog` supports checking several items in one session, checking more than one when triggered from a single row's checkbox auto-adds one new row per *additional* item (via `BuilderLine.SetExistingItemDirect`, which populates a line without re-invoking the picker) rather than silently discarding everything past the first pick.
