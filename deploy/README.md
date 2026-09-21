# VideoForensics Windows Service Deployment

This folder contains PowerShell scripts to install and uninstall the VideoForensics web application as a Windows Service.

## Prerequisites

- **Windows 10 or later** (or Windows Server)
- **Administrator privileges** (both install and uninstall scripts require `#Requires -RunAsAdministrator`)
- **.NET 10 runtime** installed on the target machine (framework-dependent deployment)

## Publishing the Application

Before installing the service, you must publish the `VideoForensics.WebApp` project:

```powershell
dotnet publish src/client/web/VideoForensics.WebApp -c Release --self-contained false -o <output-folder>
```

Example:
```powershell
dotnet publish src/client/web/VideoForensics.WebApp -c Release --self-contained false -o C:\Build\VideoForensics
```

The `-c Release` flag builds for release. The `--self-contained false` flag creates a framework-dependent deployment (smaller, since .NET 10 is already on the target machine); the published output will be approximately 50-100 MB.

The output folder will contain `VideoForensics.WebApp.exe` and supporting assemblies.

## Installation

Run the install script **as Administrator**:

```powershell
.\install-service.ps1
```

This uses default paths:
- **Published output:** `../src/client/web/VideoForensics.WebApp/bin/Release/net10.0/publish`
- **Install location:** `%ProgramFiles%\VideoForensics\`

To use custom paths:

```powershell
.\install-service.ps1 -PublishedPath "C:\Build\VideoForensics" -InstallPath "D:\Services\VideoForensics"
```

The installation script will:
1. Validate the published folder exists and contains the executable
2. Create or overwrite the install directory
3. Copy all published files to the install location
4. Create the Windows Event Log source (if needed)
5. Install the service in the Windows Service Control Manager
6. Configure automatic restart on failure
7. Start the service

The service is configured to start automatically on system boot.

## Bootstrap Installer Scripts

The two bootstrap installer scripts (`install.ps1` and `install.sh`) fetch the latest VideoForensics release for your chosen channel from GitHub at runtime and launch the platform-specific installer (MSI + Bootstrapper .exe on Windows, .deb on Debian/Ubuntu).

These are thin wrappers around the platform installers and do not perform installation themselves — they simply download and invoke the appropriate installer for your system. For detailed installation mechanics, see [`deploy/windows/README.md`](windows/README.md) (Windows) or [`deploy/debian/README.md`](debian/README.md) (Debian/Ubuntu).

### Channels

VideoForensics publishes two release channels:

- **Stable** (default): Tagged releases (`vX.Y.Z`) off the `main` branch. Assets are signed and ready for production use. Specify `-Channel Stable` (PowerShell) or `--channel stable` (bash), or omit the flag to use this channel by default.
- **Testing**: Rolling prerelease built on every push to the `dev` branch and tagged as `testing`. This channel receives new features first and is suitable for testing, but may be unstable. Specify `-Channel Testing` (PowerShell) or `--channel testing` (bash).

### Usage Examples

#### Windows (PowerShell)

Interactive installation (Stable channel, default):

```powershell
irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 | iex
```

Testing channel:

```powershell
&([scriptblock]::Create((irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1))) -Channel Testing
```

Silent installation (unattended, Stable channel):

```powershell
&([scriptblock]::Create((irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1))) -Silent
```

#### Debian/Ubuntu (Bash)

Interactive installation (Stable channel, default):

```bash
curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash
```

Testing channel:

```bash
curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash -- --channel testing
```

## Uninstallation

Run the uninstall script **as Administrator**:

```powershell
.\uninstall-service.ps1
```

This will:
1. Stop the running service (if active)
2. Remove the service from the Windows Service Control Manager
3. Leave the installed binaries in `%ProgramFiles%\VideoForensics\` (optional removal below)
4. Leave all data (`%ProgramData%\VideoForensics\`) untouched

To also remove the installed binaries:

```powershell
.\uninstall-service.ps1 -RemoveInstallPath
```

**Important:** The data directory (`%ProgramData%\VideoForensics\`) is never automatically deleted, regardless of the `-RemoveInstallPath` flag. This contains the application database, encryption keys, and settings. Delete it manually only if you are certain you want a complete clean uninstall.

## How It Works

The `VideoForensics.WebApp` executable is **the same binary** whether it runs:
- Interactively via `dotnet run` in development
- Via an IDE debugger (F5)
- As a Windows Service

The application automatically detects its hosting context at runtime via `UseWindowsService()`. There is nothing to toggle, no configuration to change, and no separate service-only project to maintain.

## Event Logging

When running as a service, diagnostic logs are written to the **Windows Event Log**.

To view logs:
1. Open **Event Viewer** (Win+R, type `eventvwr.msc`)
2. Navigate to **Windows Logs** > **Application**
3. Filter by Source: **VideoForensics**

Log entries include diagnostic information (startup, shutdown, errors) at levels configured in `appsettings.json`.

The same `appsettings.json` log-level filtering applies whether running interactively or as a service, so configuration is consistent across deployment scenarios.

## Service Details

| Property | Value |
|----------|-------|
| **Service Name** | `VideoForensics` |
| **Display Name** | `VideoForensics` |
| **Startup Type** | Automatic |
| **Run As** | Local System |
| **Failure Recovery** | Restart after 60 seconds (up to 24-hour reset window) |

## Upgrading the Service

To upgrade to a new version:

1. Publish the new build:
   ```powershell
   dotnet publish src/client/web/VideoForensics.WebApp -c Release --self-contained false -o C:\Build\VideoForensics
   ```

2. Run the install script with the new published path:
   ```powershell
   .\install-service.ps1 -PublishedPath "C:\Build\VideoForensics"
   ```

The script is safely re-runnable: it will stop and remove the old service, copy the new files, and reinstall.

## Data and State

All persistent application data is stored in `%ProgramData%\VideoForensics\`, including:
- **Database:** `VideoForensics.db` (SQLite)
- **Encryption keys:** DPAPI-protected keys for credential storage
- **Application settings:** Configuration and cache
- **Logs:** Diagnostic logs (if file logging is enabled)

This directory is shared across all hosting contexts (interactive and service) and is never touched by the uninstall script.

## Troubleshooting

**Service fails to start:**
- Check the Windows Event Log (Event Viewer > Windows Logs > Application, Source: VideoForensics) for detailed error messages.
- Ensure the .NET 10 runtime is installed on the target machine.
- Verify `%ProgramFiles%\VideoForensics\` contains all expected files.

**Can't run the install script:**
- Ensure you are running PowerShell as Administrator (`#Requires -RunAsAdministrator`).
- If PowerShell execution policy blocks the script, run: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`

**Need to remove all traces:**
- Run: `.\uninstall-service.ps1 -RemoveInstallPath` (removes service and binaries)
- Manually delete `%ProgramData%\VideoForensics\` (removes all data)

## Important Note

Installing a Windows Service is a **system-level change** that:
- Registers the application with the Windows Service Control Manager (SCM)
- Persists across system reboots
- Runs as a background process regardless of who is logged in

This is intentional for production deployments and different from interactive development workflows. Do not run the install script as part of a routine dev-loop — use it only when deliberately moving to service-based hosting.
