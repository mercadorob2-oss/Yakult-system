# Connected Devices + Prank STOP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin Portal gains a "Connected Devices" page (same presence data as the ITCM server Devices tab) where an admin can hit STOP on any online PC and make our STOP alert dialog + alarm sound pop on that PC.

**Architecture:** Reuse `dbo.CallClientPresence` heartbeats (extend the desktop reporter to beat app-wide, not just inside Call Monitoring). New `dbo.AdminAlertBroadcast` table carries prank alerts; a lightweight poller in `MainForm` picks them up and shows a standalone WinForms STOP dialog. Admin page follows the `BranchAcronymPage` list pattern.

**Tech Stack:** .NET Framework WinForms/WPF, ADO.NET + Dapper (where the file already uses it), T-SQL migrations with `IF NOT EXISTS` guards.

**Spec:** This plan IS the spec (built from codebase evidence 2026-10-14; no separate design doc — user approved the concept in chat: "connected devices like ITCM + STOP prank action").

## Global Constraints

- `Yakult.Inventory.App\Yakult.Inventory.App.csproj` is non-SDK-style: EVERY new `.cs` file MUST get an explicit `<Compile Include>` entry or the build fails invisible-file (CS0234).
- No test runner exists in the repo (no `*Test*` projects found): verification steps are MSBuild + stated manual runtime checks, not pytest.
- Migrations live in `DATABASES\Yakult-DB-Production\dbo\Scripts\` as idempotent `ALTER`/`CREATE` scripts with `IF NOT EXISTS` guards; never edit `CREATE TABLE` definition files.
- WPF XAML property `LetterSpacing` does not exist; new dialog is WinForms code-behind (no XAML) so this is moot, but no `--` inside any XML comment in edited `.csproj`/config.
- Sending STOP is admin-only: gate with `AppSession.IsDeveloper || AppSession.IsSuperAdmin || AppSession.IsAdmin` (same predicate as the nav-bar admin tools).
- Pollers must silently no-op when their table is missing (DB without the migration applied) — follow the `CallSchemaGate.TableExists` precedent.

## Review Focus

- Stale-machine STOP: admin clicks STOP on a row that just went offline; target never shows it (expiry + ack model) — acceptable, but the send-confirm must name machine+user so the admin knows what they hit.
- Double-fire: two app instances same machine+user must not pop two dialogs — ack row is per `(BroadcastId, MachineName, UserName)`, first writer wins, losers skip.
- Ancient alert on next login: `ExpiresAt` (default 10 min) filters it; test by inserting an expired row and confirming no popup.
- Sound file missing on target PC: dialog must still pop with fallback beeps (reuse the MP3-then-beeps strategy from `WpfMainDashboardShell`).
- Self-prank: admin STOPs their own machine — allowed, confirm dialog makes it explicit.

---

### Task 1: Broadcast table migration + data-access helper

**Files:**
- Create: `DATABASES\Yakult-DB-Production\dbo\Scripts\Migration_AdminAlertBroadcast_CreateTable.sql`
- Create: `Yakult.Inventory.App\Repositories\AdminAlertRepository.cs`
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (add `<Compile Include="Repositories\AdminAlertRepository.cs" />` in the Repositories block)

**Interfaces:**
- Consumes: nothing new (uses `DatabaseConfig.ConnectionString`, plain ADO.NET like `BranchAcronymPage`).
- Produces (used by Tasks 3+4):
  - `int SendStopAlert(string targetMachine /*null=all*/, string targetUser /*null=all*/, int createdBy, string createdByName, string title="STOP", string message=null, int expireMinutes=10)`
  - `List<AdminAlertDto> GetPendingAlerts(string machineName, string userName)` — returns unexpired, unacked rows matching `(TargetMachine IS NULL OR = @m) AND (TargetUser IS NULL OR = @u)`
  - `void AcknowledgeAlert(int broadcastId, string machineName, string userName)` — `INSERT` ack row only (first writer wins; catch PK violation and ignore)
  - `AdminAlertDto { BroadcastId, Title, Message, CreatedByName, CreatedAt }`

- [ ] **Step 1: Write migration SQL** in `Migration_AdminAlertBroadcast_CreateTable.sql`: `dbo.AdminAlertBroadcast` (`BroadcastId INT IDENTITY PK`, `Title NVARCHAR(100) NOT NULL DEFAULT 'STOP'`, `Message NVARCHAR(500) NULL`, `TargetMachine NVARCHAR(64) NULL`, `TargetUser NVARCHAR(128) NULL`, `CreatedBy INT NOT NULL`, `CreatedByName NVARCHAR(128) NOT NULL DEFAULT ''`, `CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`, `ExpiresAt DATETIME2 NOT NULL`) + `dbo.AdminAlertAck` (`BroadcastId INT NOT NULL`, `MachineName NVARCHAR(64) NOT NULL`, `UserName NVARCHAR(128) NOT NULL`, `AckAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`, PK over all three, FK to Broadcast), each wrapped in `IF NOT EXISTS` guards; index on `AdminAlertBroadcast(ExpiresAt)`.
- [ ] **Step 2: Implement `AdminAlertRepository`** with the three methods above; every query wrapped so a missing table returns empty/false instead of throwing (check `OBJECT_ID` first, mirroring `CallSchemaGate`).
- [ ] **Step 3: Register the file in the csproj** and validate XML parses: `try { [xml](Get-Content "Yakult.Inventory.App\Yakult.Inventory.App.csproj" -Raw) | Out-Null; Write-Host "VALID XML" } catch { Write-Host "INVALID" }`.
- [ ] **Step 4: Build** `MSBuild Yakult.Inventory.App.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU /p:OutputPath=<temp> /m /v:minimal /nologo` — Expected: exit 0, zero `error` lines.
- [ ] **Step 5: Commit** migration + repository.

### Task 2: App-wide presence heartbeat

**Files:**
- Modify: `Yakult.Inventory.App\Services\ItcmPresenceReporter.cs` (add multi-module owner support)
- Modify: `Yakult.Inventory.App\Pages\User\MainForm.cs` (start/stop app-wide beat on login/logout)

**Interfaces:**
- Consumes: existing `ItcmPresenceReporter.Start()/Stop()` call sites in Call Monitoring stay source-compatible.
- Produces (used by Task 4's device list indirectly via `dbo.CallClientPresence` rows with `Module='Workspace'`):
  - `ItcmPresenceReporter.Start(string module = "CallMonitoring")` — adds owner, ensures the single 5-min timer runs
  - `ItcmPresenceReporter.Stop(string module = "CallMonitoring")` — removes owner, stops timer only when no owners remain; keep parameterless `Stop()` stopping everything (existing callers unchanged)

- [ ] **Step 1: Read `ItcmPresenceReporter.cs` completely**, then add an owner set: each interval tick POSTs one beat per owner module (payload identical except `module` value).
- [ ] **Step 2: In `MainForm.cs`, start `ItcmPresenceReporter.Start("Workspace")` after successful login and stop `Stop("Workspace")` on logout/close** (find the login-OK and logout paths; do not move existing Call Monitoring `Start()/Stop()` calls).
- [ ] **Step 3: Build** (same MSBuild command) — Expected: exit 0.
- [ ] **Step 4: Manual check** — run app, log in, confirm a `Module='Workspace'` row appears in `dbo.CallClientPresence` for this machine within ~5 min (immediate beat on `Start` should make it seconds).
- [ ] **Step 5: Commit**.

### Task 3: Standalone STOP dialog + broadcast poller

**Files:**
- Create: `Yakult.Inventory.App\Dialogs\AdminAlertDialog.cs` (WinForms `Form`, code-built UI like `LoginPage.ApplyWorkspaceLayout`)
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (Compile Include, no SubType)
- Modify: `Yakult.Inventory.App\Pages\User\MainForm.cs` (20-second `Timer` poller while logged in)

**Interfaces:**
- Consumes: Task 1's `GetPendingAlerts`/`AcknowledgeAlert`; MP3 at `Images\Danger Alarm Sound Effect.mp3` next to exe with beep fallback.
- Produces (used by Task 4 only indirectly — Task 4 inserts rows; this task displays them):
  - `AdminAlertDialog.ShowAlert(IWin32Window owner, AdminAlertDto alert)` — static, shows dark-navy STOP dialog (blinking STOP via `Timer`, red border, Tool/From/Timestamp rows, TERMINATE SESSION + REVIEW LOGS buttons that both dismiss), plays MP3 once via `MediaPlayer` w/ beep fallback, non-modal-safe on the UI thread

- [ ] **Step 1: Write `AdminAlertDialog`** mirroring the WPF STOP dialog visuals (dark `#1F2B45`, red `#E53935` STOP banner, siren pills flashing via WinForms `Timer`, shake on open, details rows, two dismiss buttons); sound = MP3 if present else `SystemSounds.Hand` + 3 beeps on a background thread; every animation timer stopped on close.
- [ ] **Step 2: Add poller to `MainForm`**: `Timer Interval=20000`, tick (guarded: only when `AppSession.IsLoggedIn`) calls `GetPendingAlerts(Environment.MachineName, AppSession.CurrentUserName)`, for each row calls `AcknowledgeAlert` FIRST then `ShowAlert`; whole tick wrapped in try/catch-silent (never break the main form).
- [ ] **Step 3: Register csproj entry**, validate XML, **build** — Expected: exit 0.
- [ ] **Step 4: Manual check** — insert a test row (`SendStopAlert(null,null,...)`), confirm dialog pops with sound within ~20s and only once; insert an expired row, confirm silence.
- [ ] **Step 5: Commit**.

### Task 4: Connected Devices admin page + STOP prank action

**Files:**
- Create: `Yakult.Inventory.App\Pages\Admin\Monitoring\ConnectedDevicesPage.cs` (UserControl; plain `.cs`, no SubType)
- Modify: `Yakult.Inventory.App\Forms\Admin\AdminPortalForm.cs` (new `🖥 Device Monitoring` section + `ShowConnectedDevicesPage()`, following `PopulateSideMenu` bottom-to-top ordering)
- Modify: `Yakult.Inventory.App\Yakult.Inventory.App.csproj` (Compile Include)

**Interfaces:**
- Consumes: Task 1's `SendStopAlert`; presence rows via existing `CallMonitoringRepository.GetConnectedClientsAsync(15)` filtered to `Module == "Workspace"` (fallback: direct ADO.NET same SELECT if repo access is awkward — executor picks one, not both).
- Produces: nothing downstream.

- [ ] **Step 1: Write `ConnectedDevicesPage`** following `BranchAcronymPage.cs` structure (`BuildUI` with `DefaultListPageTemplate`, `DataGridView AutoGenerateColumns=false`, search + Online/Stale filter, 15s refresh `Timer` stopped on dispose): columns Status, Machine, User, Version, First seen, Last seen (+Session age if cheap), online-count header, highlight `Environment.MachineName` row as "(this device)"; per-row `STOP` button column.
- [ ] **Step 2: STOP action** — click shows confirm (`"Send STOP prank alert to MACHINE (user)?"` OK/Cancel, admin gate re-checked); on OK calls `SendStopAlert(machine, user, AppSession.CurrentUserId, AppSession.CurrentUserName)` and shows a one-line status confirmation. Never send from a stale/offline row without the confirm naming the target (Review Focus #1).
- [ ] **Step 3: Wire menu + csproj**, validate XML, **build** — Expected: exit 0.
- [ ] **Step 4: Manual check** — open Admin Portal → Connected Devices, see self row online; STOP another test PC (or self), confirm the STOP dialog + alarm fires there once.
- [ ] **Step 5: Commit**.
