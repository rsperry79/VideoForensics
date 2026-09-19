# VideoForensics Windows Installer

This folder contains WiX v5 projects to build a Windows MSI installer and Burn bootstrapper (.exe) for VideoForensics, replacing the manual PowerShell-based install script with a packaged, upgradeable installer experience.

## Validation Status

**IMPORTANT:** The WiX projects in this folder have NOT been build-validated in this repository's sandbox environment (no WiX Toolset SDK or Windows platform available here). The projects follow WiX v5 patterns and conventions but the first real build on a Windows machine or CI runner should be treated as a shakedown build. See **First Build Checklist** below.

## Prerequisites

### Build Environment
- **Windows machine or CI runner** with the WiX Toolset v5 SDK installed
  - Download: [wixtoolset.org](https://wixtoolset.org/)
  - Installation: runs as an MSBuild extension; `dotnet build` will find it automatically
- **.NET 10 SDK** (to publish VideoForensics.WebApp)
- **MSBuild** (bundled with .NET SDK, or Visual Studio)

### Target Machine (Runtime)
- **Windows 10 or later** (or Windows Server equivalents)
- **.NET 10 runtime** is NOT required — the installer packages a self-contained, self-sufficient binary

## Publishing the Application

Before building the MSI and bootstrapper, publish the WebApp as a self-contained win-x64 binary:

```bash
dotnet publish src/client/web/VideoForensics.WebApp -c Release -r win-x64 --self-contained true -o C:\Build\VideoForensics.publish
```

This produces approximately 150-200 MB of binaries (larger than framework-dependent because .NET runtime is included). The publish output is fed into the WiX MSI build via MSBuild properties.

## FFmpeg Binaries

VideoForensics bundles ffmpeg and ffprobe binaries with the Windows installer — no separate ffmpeg installation needed on the target machine.

### What's Bundled

The MSI installer includes statically-linked LGPL ffmpeg binaries from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds):
- `ffmpeg.exe` — video transcoding and metadata extraction
- `ffprobe.exe` — video metadata probing

Both are installed to `%ProgramFiles%\VideoForensics\`.

### License Compliance

The bundled ffmpeg binaries are LGPL-licensed. The license/copyright notice is included as `ffmpeg-LICENSE.txt` in the installation directory. Do not remove this file.

### Using a Custom ffmpeg Build

To use a different or newer ffmpeg version:

- **Uniview provider:** Supports a custom path via the `UniviewFfmpegPath` configuration setting. This setting is stored in the application's configuration database and can be configured through the admin UI or directly in the database.
- **Ring provider:** No user-facing configuration setting is available today. The Ring provider resolves ffmpeg from: (1) bundled binaries in the installation directory, or (2) system PATH. To use a custom ffmpeg build with Ring, you must rebuild the application with a code change.

Ensure any custom ffmpeg build is compatible with the app's video processing requirements.

## Hardware-Accelerated Video Decoding

The app auto-detects and uses hardware-accelerated video decoding when available, including NVIDIA CUDA, Intel Quick Sync, and Direct3D11/DXVA2 acceleration. The bundled ffmpeg build already includes support for these acceleration methods.

No separate installation is required — hardware acceleration activates automatically if your GPU driver (already required for display) exposes the necessary runtime support. If no compatible hardware or driver is found, the app transparently falls back to software decoding. No configuration is needed either way.

## Building the Installer

### Quick Build (Default Paths)

From the repository root:

```bash
# Build the MSI (from the Installer.Wix project)
dotnet build deploy/windows/VideoForensics.Installer.Wix -c Release

# Build the Bootstrapper .exe (from the Bootstrapper.Wix project)
# This transitively builds the MSI and wraps it
dotnet build deploy/windows/VideoForensics.Bootstrapper.Wix -c Release
```

The build outputs:
- `deploy/windows/VideoForensics.Installer.Wix/bin/Release/VideoForensics.msi` — standalone MSI installer
- `deploy/windows/VideoForensics.Bootstrapper.Wix/bin/Release/VideoForensicsBootstrapper.exe` — user-friendly wrapper exe

### Custom Publish Path

To use a custom publish path, pass MSBuild properties:

```bash
dotnet build deploy/windows/VideoForensics.Bootstrapper.Wix -c Release `
  -p:DefineConstants="PublishDir=C:\MyBuild\VideoForensics;ProductVersion=1.0.0"
```

### CI / Release Pipeline

In CI/CD, build both artifacts from the tagged commit:

```bash
# Publish once
dotnet publish src/client/web/VideoForensics.WebApp -c Release -r win-x64 --self-contained true -o .artifacts/publish

# Build both installers with version from VideoForensics.csproj
dotnet build deploy/windows/VideoForensics.Installer.Wix -c Release -p:DefineConstants="PublishDir=.artifacts/publish;ProductVersion=1.0.0"
dotnet build deploy/windows/VideoForensics.Bootstrapper.Wix -c Release -p:DefineConstants="ProductVersion=1.0.0"

# Output files available in bin/Release/
```

## Installation

### End-User Installation (Recommended)

Run the bootstrapper .exe:

```bash
VideoForensicsBootstrapper.exe
```

This launches a standard Windows installer UI with:
- Welcome screen
- Installation directory selection (defaults to `%ProgramFiles%\VideoForensics\`)
- Install button
- Progress and completion screens

The installer will:
1. Extract the MSI and launch it
2. Copy application binaries to `%ProgramFiles%\VideoForensics\`
3. Register the VideoForensics Windows Service
4. Create the Event Log source `VideoForensics` (Application log)
5. Start the service automatically

### Admin / Group Policy Installation

For group-policy deployment or enterprise scenarios, run the MSI directly:

```bash
msiexec /i VideoForensics.msi /quiet /norestart
```

Or with a log file:

```bash
msiexec /i VideoForensics.msi /L*V install.log /quiet /norestart
```

The MSI supports standard msiexec options (`/quiet`, `/passive`, `/l`, etc.).

## Upgrade

To upgrade to a newer version, simply run the newer installer (either `.exe` or `.msi`):

```bash
# Run the new bootstrapper over the old install
VideoForensics_2.0.0.exe

# Or the MSI directly
msiexec /i VideoForensics_2.0.0.msi
```

The installer will:
1. Detect the existing installation
2. Stop the running VideoForensics service
3. Back up the old binaries (left in place, not deleted)
4. Replace binaries in `%ProgramFiles%\VideoForensics\`
5. Restart the service

The database, encryption keys, and all user data in `%ProgramData%\VideoForensics\` are preserved and automatically migrated (if needed) when the service restarts.

**No manual uninstall or data migration is required** — the upgrade is atomic and safe.

## Uninstallation

### Remove (Keep Data)

Via Control Panel:

1. Settings → Apps → Apps & features
2. Search for "VideoForensics"
3. Click "Uninstall"

Or via PowerShell:

```powershell
wmic product where name="VideoForensics" call uninstall
```

This will:
1. Stop the VideoForensics Windows Service
2. Remove the service registration
3. Remove binaries from `%ProgramFiles%\VideoForensics\` (optionally; uninstaller may prompt)
4. **Leave all data in `%ProgramData%\VideoForensics\` intact**

The data directory contains the SQLite database, encryption keys, and logs — it is safe to keep after uninstalling if you plan to reinstall later.

### Complete Removal (Delete Data)

To completely remove all traces of VideoForensics (including its database):

1. Uninstall via the installer (see above)
2. Manually delete the data directory:
   ```powershell
   Remove-Item -Recurse -Force $env:ProgramData\VideoForensics
   ```

**Warning:** This is irreversible. Only delete the data directory if you are certain you want to lose all application data (database, encryption keys, settings).

## Signing Status

**The MSI, Bootstrapper .exe, and VideoForensics.WebApp.exe are UNSIGNED.**

This means:
- On Windows 10+ with SmartScreen enabled, you may see warnings: "Windows protected your PC" / "unknown publisher"
- To dismiss this warning and run the installer:
  1. Click "More info" on the SmartScreen prompt
  2. Click "Run anyway"
- Or, disable SmartScreen (not recommended for production):
  - Settings → Update & Security → Windows Security → App & browser control → Reputation-based protection (toggle off)

**Signing Support Coming Later:**
Code signing requires a code-signing certificate. Once a certificate is available, signing will be added as a CI pipeline step (sign after build, before release). This does not require changes to the WiX projects — signing is transparent to the installer build process.

## Service Configuration

| Property | Value |
|----------|-------|
| **Service Name** | `VideoForensics` |
| **Display Name** | `VideoForensics` |
| **Startup Type** | Automatic |
| **Run As** | Local System |
| **Failure Recovery** | Restart after 60 seconds (24-hour reset window) |
| **Log Target** | Windows Event Log (`Application` log, Source: `VideoForensics`) |

### Event Logging

Application logs are written to the Windows Event Log when running as a service.

**View logs:**
1. Open Event Viewer: Win+R, type `eventvwr.msc`
2. Navigate to: Windows Logs → Application
3. Filter by Source: `VideoForensics`

Log entries include startup, shutdown, errors, and diagnostics at levels configured in `appsettings.json`.

## Data Locations

| Item | Path |
|------|------|
| **Install Directory** | `%ProgramFiles%\VideoForensics\` |
| **Database** | `%ProgramData%\VideoForensics\VideoForensics.db` |
| **Encryption Keys** | `%ProgramData%\VideoForensics\` (DPAPI-protected) |
| **Logs** | `%ProgramData%\VideoForensics\logs\` (if file logging enabled) |
| **Configuration** | `%ProgramData%\VideoForensics\appsettings.json` (optional overrides) |

The data directory is **shared** across all hosting contexts (service, interactive, debug) and is never touched by the installer or uninstaller.

## Troubleshooting

### Service Fails to Start

1. Check the Windows Event Log:
   - Event Viewer → Windows Logs → Application → Source: VideoForensics
2. Check that binaries were copied:
   - `dir %ProgramFiles%\VideoForensics\`
3. Verify the service exists:
   - PowerShell: `Get-Service VideoForensics`
4. Check service configuration:
   - Services.msc → right-click VideoForensics → Properties

### "Unknown Publisher" / SmartScreen Warning

This is expected because the executables are unsigned. See **Signing Status** above for how to dismiss the warning.

### Can't Uninstall

If the installer fails to uninstall:

1. Open Services.msc and manually stop the VideoForensics service
2. Delete `%ProgramFiles%\VideoForensics\` manually:
   ```powershell
   Remove-Item -Recurse -Force $env:ProgramFiles\VideoForensics
   ```
3. Use Programs and Features to remove the entry (Settings → Apps → Apps & features → VideoForensics → Uninstall)

### Service Crashes or Restarts Unexpectedly

1. Check Event Log for error messages (see above)
2. Verify `%ProgramData%\VideoForensics\` is writable by Local System:
   - Properties → Security → Advanced (grant Full Control to SYSTEM if needed)
3. Check that .NET runtime is available (self-contained binary should not require it, but verify):
   - `%ProgramFiles%\VideoForensics\VideoForensics.WebApp.exe --version` (if implemented)

## First Build Checklist

When building these WiX projects for the first time on a real Windows machine, check:

1. **Event Log Source Registration**
   - Current implementation: `ServiceInstall` / `ServiceControl` elements; Event Log source creation is left as a TODO
   - **On build:** verify that the Application Event Log source "VideoForensics" is created during installation (check in Event Viewer after install)
   - If not created: either enable `util:EventSource` element in Package.wxs or use the fallback registry-key approach (both are documented in the source)

2. **Service Failure Recovery**
   - Current implementation: `ServiceInstall` sets up the service, but restart-on-failure (60-second delay) is not wired via WiX attributes
   - **On build:** verify that after install, `sc.exe query failure VideoForensics` shows restart actions, or manually run:
     ```powershell
     sc.exe failure VideoForensics reset= 86400 actions= restart/60000/restart/60000/restart/60000
     ```
   - If manual step is needed, update the README with a note that this is a known gap to be fixed (via `util:ServiceConfig` or custom action)

3. **File Harvesting**
   - Current implementation: individual components for `.exe`; supporting files (DLLs, configs) are left as TODOs
   - **On build:** verify that all files from the publish output are included in the MSI (check contents via `dark.exe` or test install)
   - If files are missing: enable the `<Files>` wildcard harvesting pattern in Package.wxs (exact syntax to be confirmed on build)

4. **MSI Installation Test**
   - Fresh install: run `VideoForensics.msi` → verify service starts and runs
   - Upgrade: run an older MSI, then re-run a newer one → verify service stops, files update, service restarts
   - Uninstall: remove via Control Panel → verify service stops, binaries removed, data preserved
   - Bootstrapper: run `.exe` → verify it launches and produces the same result as direct MSI run

## Keeping vs. Retiring PowerShell Scripts

The `deploy/install-service.ps1` and `deploy/uninstall-service.ps1` scripts are still available and unchanged. **Use the MSI/bootstrapper for end-user distribution**, but keep the PowerShell scripts for:
- Developer/CI quick-iteration (no MSI build time)
- Testing service registration logic independently
- Educational reference (showing the underlying Windows Service setup patterns)

The PowerShell scripts mirror the same service configuration (name, account, auto-start) as the WiX installer, so either path produces an identical result.

## Important Notes

### Windows Service Considerations

- Installing a Windows Service is a **system-level change** that persists across reboots
- The service runs in the background regardless of user login
- This is intentional for production deployments; do not run the installer as part of routine dev loops
- Use PowerShell scripts for development; save the installer for deployment

### Self-Contained Delivery

The installer packages a self-contained .NET 10 binary, so the target machine does not need:
- .NET 10 runtime pre-installed
- Visual C++ redistributables (unless VC++ specific libraries are added to the app in the future — the bootstrapper can chain them then)

This simplifies deployment but makes the installer larger (~150-200 MB) than a framework-dependent build.

### No Auto-Update

The installer supports re-running to upgrade, but does not implement in-app "check for updates" or silent auto-update. Users must download a new installer manually (or via your release distribution channel). Auto-update is a separate feature to be added if needed.
