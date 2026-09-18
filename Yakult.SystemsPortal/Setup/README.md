# Yakult Systems Portal — Server Setup

This folder contains tools and scripts for deploying and managing the Yakult RDP launcher infrastructure. The portal web app generates `.rdp` files; the server must have `YakultLauncher.exe` configured as the alternate shell for RDP connections.

## Architecture

```
┌─────────────┐     .rdp file      ┌──────────────┐
│   Portal    │ ────────────────> │   Client PC  │
│  (Web App)  │                    │  (mstsc.exe) │
└─────────────┘                    └──────┬───────┘
                                          │ RDP Connection
                                          │ (alternate shell)
                                          ▼
                                  ┌──────────────────┐
                                  │  Target Server   │
                                  │  (192.168.100.186)│
                                  │                  │
                                  │ YakultLauncher   │
                                  │       │          │
                                  │       ├─> explorer.exe (desktop)
                                  │       └─> Yakult.Inventory.App.exe
                                  └──────────────────┘
```

## How the RDP Launch Works

1. User downloads `.rdp` file from the portal
2. Double-clicking opens `mstsc.exe` which connects to the server
3. RDP replaces the user's shell with `C:\YakultLauncher.exe`
4. The launcher:
   - Shows a splash screen: "Starting Yakult Inventory System..."
   - Starts `explorer.exe` (desktop shell)
   - Waits 5 seconds (configurable)
   - Launches `C:\Program Files\Yakult\Yakult.Inventory.App.exe`
   - Closes splash screen
   - Stays alive while Explorer runs (keeps RDP session open)
5. When user logs off, Explorer exits → launcher exits → RDP disconnects

## Quick Start

### For First-Time Setup

**1. Deploy the launcher from your dev machine:**
```powershell
cd "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.SystemsPortal\Setup"
.\Deploy-Launcher.ps1
```

**2. Run server setup ON the target server (as Administrator):**
```powershell
# Copy Setup folder to server first, then:
.\Setup-Server.ps1
```

**3. Validate the deployment from your dev machine:**
```powershell
.\Test-RdpSetup.ps1
```

**4. Test the RDP connection:**
- Download the `.rdp` file from the portal
- Double-click to connect
- The splash screen should appear, then the desktop, then the Yakult app

### For Updates (Launcher Only)

```powershell
cd "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.SystemsPortal\Setup"
.\Deploy-Launcher.ps1 -Force
```

## Deployment Safety

`Setup\Publish-Portal.ps1` uses `robocopy /MIR` for portal-managed files, but explicitly excludes the server-preserved `wwwroot\media` tree—including `wwwroot\media\employee-resources`—and `installer` from the mirror. This means the IIS deployment target keeps uploaded Employee Resources PDFs and other approved attachments, as well as its server-preserved installer contents, at `C:\inetpub\wwwroot\Yakult.SystemsPortal\wwwroot\media\employee-resources` and `C:\inetpub\wwwroot\Yakult.SystemsPortal\installer`; these files are not overwritten or deleted by the desktop **Publish & Deploy Yakult Portal** shortcut. Update server-managed attachments or installer contents separately when an intentional release is required.

## Scripts Overview

### Deploy-Launcher.ps1
**Run from:** Development machine  
**Purpose:** Builds and deploys YakultLauncher.exe to the target server

**Parameters:**
- `-Server`: Target server hostname/IP (default: IHSServer)
- `-DestinationPath`: Remote path (default: C$\YakultLauncher.exe)
- `-Configuration`: Build config (default: Release)
- `-SkipBuild`: Use existing binaries, don't rebuild
- `-Force`: Overwrite existing deployment

**Example:**
```powershell
.\Deploy-Launcher.ps1 -Server "192.168.100.186" -Force
```

### Setup-Server.ps1
**Run from:** Target server (as Administrator)  
**Purpose:** Configures server prerequisites for RDP launcher

**What it does:**
- Enables Remote Desktop
- Configures Windows Firewall
- Creates required directories (`C:\Program Files\Yakult\`)
- Sets appropriate permissions
- Adds users to Remote Desktop Users group
- Validates configuration

**Parameters:**
- `-YakultAppPath`: App installation path (default: C:\Program Files\Yakult)
- `-AllowedUsers`: Users/groups to grant RDP access
- `-SkipRdpEnable`: Skip Remote Desktop enablement
- `-SkipFirewall`: Skip firewall configuration

**Example:**
```powershell
.\Setup-Server.ps1 -AllowedUsers @("DOMAIN\InventoryUsers", "DOMAIN\RDPUsers")
```

### Test-RdpSetup.ps1
**Run from:** Any machine with network access  
**Purpose:** Validates the complete RDP setup

**Tests:**
- Network connectivity (ping, DNS)
- RDP port availability (3389)
- File deployment (launcher and app)
- Configuration validation

**Example:**
```powershell
.\Test-RdpSetup.ps1 -Server "192.168.100.186"
```

### Set-LauncherConfig.ps1
**Run from:** Target server  
**Purpose:** Configure launcher behavior via environment variables

**Environment Variables:**
- `YAKULT_APP_PATH`: Override app path (default: C:\Program Files\Yakult\Yakult.Inventory.App.exe)
- `YAKULT_STARTUP_DELAY_SECONDS`: Override startup delay (default: 5)

**Example:**
```powershell
# Change app location
.\Set-LauncherConfig.ps1 -YakultAppPath "D:\Apps\Yakult.Inventory.App.exe"

# Increase startup delay to 10 seconds
.\Set-LauncherConfig.ps1 -StartupDelaySeconds 10

# Reset to defaults
.\Set-LauncherConfig.ps1 -ClearAll
```

## Prerequisites

### Target Server
1. **Windows Server 2016 or later** (or Windows 10/11)
2. **Remote Desktop enabled** (Setup-Server.ps1 handles this)
3. **Administrative access** for deployment
4. **Network access** from client machines (port 3389)

### Development Machine
1. **.NET 8 SDK** (for building the launcher)
2. **PowerShell 5.1+** (for running deployment scripts)
3. **Administrative access** to target server (for admin share copy)

## Building the Launcher Manually

If you need to build the launcher without using Deploy-Launcher.ps1:

```powershell
cd "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.SystemsPortal\Setup\YakultLauncher"
dotnet publish -c Release -o publish
```

This produces a single self-contained executable at `publish\YakultLauncher.exe` (~33 MB, includes .NET 8 runtime).

## Manual Deployment (Without Scripts)

If you prefer manual deployment:

```powershell
# 1. Build the launcher
cd "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.SystemsPortal\Setup\YakultLauncher"
dotnet publish -c Release -o publish

# 2. Copy to server
Copy-Item .\publish\YakultLauncher.exe "\\IHSServer\C$\YakultLauncher.exe" -Force

# 3. Create app directory on server
New-Item -Path "\\IHSServer\C$\Program Files\Yakult" -ItemType Directory -Force

# 4. Install Yakult.Inventory.App.exe to that directory
# (Manually copy or run installer on the server)
```

## Configuration

### Portal Configuration (appsettings.json)

The portal's RDP configuration is in `appsettings.json`:

```json
{
  "Portal": {
    "Rdp": {
      "ServerAddress": "192.168.100.186",
      "LauncherPath": "C:\\YakultLauncher.exe",
      "Username": "",
      "Domain": "",
      "ScreenWidth": 1920,
      "ScreenHeight": 1080,
      "ColorDepth": 32,
      "PromptForCredentials": true
    }
  }
}
```

### Launcher Configuration (Environment Variables)

Set on the **target server**:

```powershell
# System-wide (requires admin)
[Environment]::SetEnvironmentVariable("YAKULT_APP_PATH", "D:\Apps\Yakult.Inventory.App.exe", "Machine")
[Environment]::SetEnvironmentVariable("YAKULT_STARTUP_DELAY_SECONDS", "10", "Machine")

# Or use the helper script
.\Set-LauncherConfig.ps1 -YakultAppPath "D:\Apps\Yakult.Inventory.App.exe" -StartupDelaySeconds 10
```

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `.rdp` opens then closes immediately | `YakultLauncher.exe` not deployed | Run `.\Deploy-Launcher.ps1` |
| Splash screen appears but app doesn't launch | App not installed at expected path | Install app or run `.\Set-LauncherConfig.ps1 -YakultAppPath <new-path>` |
| "The system cannot find the file specified" | App path incorrect | Verify path with `Test-RdpSetup.ps1` |
| "Access denied" when starting app | User lacks permissions | Grant Read & Execute on app directory |
| Launcher starts but desktop never appears | Explorer.exe disabled/corrupt | Run `sfc /scannow` on server |
| RDP session ends immediately | Launcher crashed | Check `C:\YakultLauncher.log` |
| App launches but desktop is blank | Explorer.exe failed to start | Check launcher log, restart server |
| Slow startup | Delay too long or app slow to load | Reduce delay: `.\Set-LauncherConfig.ps1 -StartupDelaySeconds 3` |

### Log File Location

The launcher writes logs to: **`C:\YakultLauncher.log`**

Check this file for detailed error messages and startup sequence.

### Common Log Messages

```
[YakultLauncher] YakultLauncher starting
[YakultLauncher] Yakult app path  : C:\Program Files\Yakult\Yakult.Inventory.App.exe
[YakultLauncher] Startup delay   : 5s
[YakultLauncher] Started explorer.exe (PID 1234)
[YakultLauncher] Started Yakult app (PID 5678)
[YakultLauncher] Waiting for explorer.exe to exit...
```

## Security Considerations

1. **Admin Share Access**: Deployment uses `C$` admin share. Ensure only authorized users have admin access.
2. **RDP Security**: Enable Network Level Authentication (NLA). Setup-Server.ps1 does this by default.
3. **User Permissions**: Grant minimum necessary permissions. Use specific AD groups instead of "Everyone".
4. **Firewall**: Only open port 3389 to trusted networks.
5. **App Permissions**: The Yakult app directory should grant Read & Execute to RDP users, not Full Control.

## Legacy Scripts

The `Legacy/` folder contains scripts from earlier development:
- `Publish-YakultRemoteApp.ps1`: For RD Connection Broker deployments (requires Active Directory domain)

These are **not needed** for the current workgroup-based deployment but are kept for reference.

## Version History

- **v1.1.0** (Current): Added splash screen, automated deployment scripts, improved error handling
- **v1.0.0**: Initial launcher implementation

## Support

For issues:
1. Check `C:\YakultLauncher.log` on the server
2. Run `Test-RdpSetup.ps1` to validate configuration
3. Review troubleshooting table above
4. Check Event Viewer on server: `Applications and Services Logs → Microsoft → Windows → RemoteDesktopServices-RdpCoreTS`
