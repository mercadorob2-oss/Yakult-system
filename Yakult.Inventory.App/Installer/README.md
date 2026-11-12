# Desktop updates and installer releases

## Unified release and deployment app

Open **Yakult Release Desk** on the Desktop, or run `Run-YakultReleaseDesk.cmd`. This standalone WPF app uses Windows PowerShell 5.1 in STA mode, with `ReleaseDesk.xaml` for the interface and `ReleaseDesk.Core.ps1` for inventory queries. It does not require building the main Yakult application.

The workspace includes an overview, searchable script cards, a file details panel with a read-only preview, shortcut target inspection, favorites, recently opened tools, release actions, live activity output, and folder preferences. **F5** refreshes the catalog; **Ctrl+K** opens search. Select a card to inspect it, then use **Open tool**, **Show location**, or **Copy full path**. Favorites, recent launches, the publish folder, and window size are saved in `%LOCALAPPDATA%\Yakult\ReleaseDesk\workspace.json`.

Both the Desktop `publish folder` and this Installer folder are scanned recursively. The library includes PowerShell scripts/modules/manifests, command/batch scripts, file/Internet shortcuts, Inno definitions, SQL, VBScript, JavaScript, WSF, and shell scripts. Filters distinguish the source folder and current/archived copies. **Folders & preferences** changes the publish toolbox location. Internal helper scripts also appear because the catalog covers the complete folders. File previews show the first 100 lines; descriptions summarize the file name and type. General tools open in their associated application, and PowerShell scripts open in an interactive console.

**Desktop release** guides preparation/build, package verification, and publication. Choose a completed package explicitly for preview or publication; these actions preserve its signed saved notes and do not use the draft version/notes. The confirmation identifies the exact version and destination. Draft fields are saved with your workspace. Build actions validate version components (0 through 65534) and the 16,000-character notes limit before starting. A checkout-wide build lease prevents concurrent release builds from different launchers. Publication retains the existing shared-feed lock and verification protections. No release or deployment runs automatically when opening or refreshing the app.

The inventory labels packages **Files present** when the installer and `latest.json` exist. **Verify package and check feed** runs in a separate process, checks signed metadata, product/version/name, installer size and SHA256, and compares the signed published feed. It uses only the public key. Results show their inspection time and saved notes; publication always rechecks. Inspection has a 30-second timeout. A matching feed means the release is announced, not that the remote installer was independently checked. Preview publication does not test write permissions or EXE rename restrictions; actual publication performs those checks.

**Activity & output** shows live output, elapsed time, and the latest 100 saved job records. Per-job `.log` and atomically updated `.json` records are kept in `%LOCALAPPDATA%\Yakult\ReleaseDesk\Jobs`, including action, version, destination, start/finish times, process ID and exit status. **Open saved logs** opens that folder. The live display is bounded; full job output remains on disk. Records left Running by an interrupted process remain available for diagnosis; they do not prove a job is still active. Builds/publications must finish before closing the window; safe cancellation of write operations is not provided.

The library details pane is resizable, and the window supports a 960 by 600 minimum. Folder catalog scans and script previews still run synchronously. `-UpdateFolder` can select an alternate feed when starting the desk.

Run `tools\Test-ReleaseDesk.ps1` with Windows PowerShell 5.1 in STA mode and `-ExecutionPolicy Bypass` to check signature/tamper rejection, version rules, atomic history updates, exclusive locking, XML/PowerShell parsing, and real WPF job failure/log recovery. It uses synthetic local fixtures and, when available, reads an existing signed local build against its local metadata; it never publishes or accesses the shared feed.

The desktop checks this folder at startup and every 15 minutes:

```text
\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions
```

Use the UNC path, since other PCs may map `Y:` differently. All intended users need read access; release publishers need write access. The source can be changed with `DesktopUpdateShare` in the installed EXE configuration.

## First installation

Build and install the first updater-enabled release, version **1.6.0.0**, once on each PC. This introduces the dashboard's **Updates** notification and `Yakult.DesktopUpdater.exe`. It uses the repository's existing per-user Inno AppId `{98EE8B2E-10D8-4524-A66B-FE50D449AEA4}`. Older installers with other AppIds do not acquire update support until this first installation. Development builds and unrelated installation identities are deliberately excluded from automatic replacement.

The default install directory is `%LOCALAPPDATA%\Yakult Inventory Monitoring System`. Existing installations with the same AppId retain their directory. Existing `appsettings*.json` and Windows user settings are preserved. Manual and automatic upgrades merge existing EXE `appSettings` (including API addresses and `DesktopUpdateShare`) and connection strings into the new configuration, keeping its new binding redirects and adding newly introduced defaults. Assembly identity stays at 1.0.0.0 to preserve user settings, while FileVersion changes for each release. IT must manage machine-wide installations separately.

The intended working database is **`Yakult_Inventory_System_DEV`** on **`192.168.100.186,50301`**. The `_DEV` suffix is historical; **`YIMS_PROD` is the dummy database**. Normal builds package the canonical `Yakult.Inventory.App\appsettings.json` as the base default. Additional `appsettings.Development.json`/other environment files are excluded so they cannot override that selection on a fresh PC. `-DatabaseSettingsPath 'C:\secure\deployment.json'` can explicitly choose a different base file containing `ConnectionStrings.DefaultConnection`. Existing base files and per-user overrides remain preserved during upgrades. An update may introduce the base file when absent, while removing newly introduced environment overrides.

**Ctrl+Shift+D** opens native WPF connection properties from startup/login and the dashboards. The window identifies the active connection source and working/dummy/custom environment. **Use default** loads the packaged default; **Test connection** must succeed before **Save & restart** is available. Connection values are masked until **View all** verifies an active `dbo.[User]` developer account's password against the default database (or the active connection when no default exists). Verification supports existing salted hashes and legacy password storage, changes no login/session state, and performs no database writes. **Hide all** and closing the window clear plaintext details. A developer role alone does not bypass password verification.

If a manual upgrade cannot merge its EXE settings, it keeps `Yakult.Inventory.App.exe.config.before-update` beside the installed configuration and fails setup. Recover that backup before retrying. Automatic updates retain unresolved `saved-*` settings backups in their local job folder; cache cleanup never removes them.

## Build an installer without opening Inno Setup

Double-click **Build-DesktopInstaller.cmd** and enter a version such as `1.6.1.0`. Or run:

```powershell
.\Build-DesktopRelease.ps1 -Version 1.6.1.0 -ReleaseNotes 'Fixes and improvements'
```

The script builds the desktop and updater, stamps the application FileVersion, and invokes the installed Inno Setup compiler. It creates `artifacts\desktop-releases\<version>\YISP_Setup_<version>.exe` and signed `latest.json`. Visual Studio MSBuild with .NET Framework 4.8 desktop tooling and Inno Setup 6.3+ are needed on the build PC. No compiler is needed on users' PCs.

## Publish the release

Double-click **Publish-DesktopInstaller.cmd**. It automatically selects the highest numeric version among completed builds in `artifacts\desktop-releases`, verifies its signed build metadata and installer checksum, and preserves its saved release notes. There are no installer-path or version prompts. An incomplete build without an installer and `latest.json` is skipped; a selected build with invalid metadata or changed installer bytes stops publication. Running it again for an unchanged published release reports **already published** and leaves the feed unchanged. If the feed is newer than every local build, it reports **nothing to publish**.

For a read-only preview, run `Publish-DesktopRelease.ps1 -WhatIf`. To select a particular completed build, pass `-Version 1.6.4.0`; explicit `-InstallerPath`/`-Version` arguments remain supported for custom installers. Or build and publish together:

```powershell
.\Build-DesktopRelease.ps1 -Version 1.6.1.0 -ReleaseNotes 'Fixes and improvements' -Publish
```

Publishing copies and hashes the installer before switching `latest.json` atomically. The previous metadata remains in `latest.previous.json`. Every release needs a higher version; existing package filenames are immutable. To recover from a bad release, publish a corrected build with a higher version. Merely copying an EXE into the share does not announce an update. For manually built installers, use `Publish-DesktopRelease.ps1` and ensure their AppId, packaged updater, application FileVersion and supplied release version match this pipeline.

## Release signing

Clients verify RSA-SHA256 signed metadata using the public key embedded in the desktop and updater. The original signed envelope is staged as `release.json`; the helper independently verifies it and matches it against the selected release before signaling readiness. It verifies the installer byte length and SHA256 again, and holds the installer read-only during execution. An unsigned, altered or partial feed or local job is rejected. The installer itself can optionally use Windows Authenticode via `-SignTool` and an Inno-configured signing tool; RSA metadata signing works with the current unsigned installer workflow.

The private release key is stored at `%LOCALAPPDATA%\Yakult\DesktopReleaseSigning\manifest-private.xml` on the release PC, readable only by that Windows user and SYSTEM. **Back it up securely through IT.** Never copy it into the update share, repository, installer or application output. Only `UpdatePublicKey.xml` is distributed. A second release PC must obtain the existing private key securely; generating a replacement would break trust for installed clients. `Initialize-DesktopReleaseSigning.ps1` initializes a new key only when the checkout has no public key yet, and otherwise checks for the matching existing private key.

## Update experience

An available signed release changes the Workspace header to **Updates (1)**. A single app-wide toast also announces each new version while users are in other modules, respecting their notification preference. Its dialog shows the installed version, release notes and an **Update now** action. Download percentage reflects copied bytes. The app stays usable if the share is unavailable; checks time out without blocking the UI or accumulating network tasks.

After download verification, a separate local updater displays installation progress. Yakult requests a normal exit, respecting its existing unsaved-work close guard. A cancelled exit cancels installation; the updater never kills the app or other sessions. Another process using the same installation blocks the update. The installer runs silently, without rebooting Windows. The updater restores local JSON settings, checks both the installer result and installed FileVersion, and reopens the app. On the next dashboard, a one-time **Update complete! Congratulations!** dialog confirms the installed version. Failed, cancelled or reboot-required installations never produce a success receipt.

Installer logs and pending jobs are local to `%LOCALAPPDATA%\Yakult\DesktopUpdates`. Installer payloads are removed after success, failure or cancellation; the running helper is removed on a later scan. Cache scans run at startup and before downloads, retain five completed jobs for up to 30 days, and clean inactive downloads older than one day. Active jobs are protected by an exclusive cache lease. Recent unconsumed receipts and any settings backups awaiting recovery are retained. Maintenance only touches recognized files in direct GUID-named job folders and never recursively deletes directories or follows directory junctions. Database migrations remain centrally managed; releasing an installer does not run SQL migrations.

## Validation

If publication reports that the share denied an EXE installer, ask the file-server administrator to allow `.exe` installer files in the update folder and verify your account has Modify access. A share may permit temporary uploads while blocking their rename to `.exe`; the publisher checks this before uploading the full installer. Running the script as a local administrator does not remove a server restriction. Once access is corrected, rerun `Publish-DesktopInstaller.cmd` with the same installer path and version. No rebuild is needed after a failed publication, and `latest.json` is only changed after the installer is available.

The `ypi-hqfs` deployment has a narrow FSRM exception at `E:\YPI-HQFS\My_Dept\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions` (the server-local path of the update share). Its custom file group, `Yakult Desktop Update Installers`, permits only `YISP_Setup_*.exe` and `YISP_PublishProbe_*.exe`. The parent IT folder's executable/system-file screen remains active, including for unrelated EXEs in the feed. Preserve this folder-specific exception when migrating the feed; do not disable the parent screen or broaden the built-in file groups.

`tools\Test-DesktopUpdates.ps1` uses synthetic installer bytes and a fake process host. It checks signatures, local job tampering, unsafe paths, version rules, publication ordering, production database selection, EXE configuration merging, restoration failures, cache leases/retention, cancellation, open sessions, installer failures and completion receipts. It also calls `tools\Test-DesktopReleasePackage.ps1`, which compiles and runs tiny Inno fixtures inside the ignored test artifacts folder, with a unique AppId, no uninstall registration, no shortcuts, and no application launch. This verifies the real file-selection and manual-upgrade hooks against synthetic settings. Inno Setup 6 is required. No Yakult application installation or business database is opened. A real standard-user pilot on a separate PC is still required before broad rollout.

`tools\Test-DesktopUpdaterBuild.ps1` cleans the helper's Debug and Release outputs, then exercises the app's build/copy target with Visual Studio reference building disabled and output paths containing spaces. The helper is a build dependency rather than a managed assembly reference. Its output is obtained from MSBuild instead of assuming a `bin\<configuration>` location.

`tools\Test-DatabaseConnectionSettings.ps1` builds the full desktop application and checks the connection profile, working/dummy classification, password punctuation, authentication changes, developer password/role rules, masked/revealed WPF controls, default reset, retest requirements and the packaged default catalog. It renders synthetic UI previews under ignored artifacts without opening SQL connections, saving settings, changing sessions or launching Yakult.

## Grouped tools

**Grouped tools** organizes related scripts into named sections independently of Favorites. On first use, starter groups suggest Desktop releases, Portal deployment, Backups & recovery, and Server setup based on current filenames. These are editable suggestions; future refreshes do not overwrite your groups.

Use **New group** with the group-name field to create a section. Choose a specific group to rename or delete it. In any library view, select a tool, choose a destination under **Add to a group**, and click **Add to group**. One tool can belong to several groups. In Grouped tools, select a tool and click **Remove** to remove that membership. Deleting a group or removing membership never deletes files or changes Favorites. **All groups** shows section headers; search and source/archive filters still apply. Empty groups remain selectable for editing. Missing files keep their saved membership and reappear when discovered again. Groups are saved in the existing per-user `workspace.json`.
