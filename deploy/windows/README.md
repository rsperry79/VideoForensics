# VideoForensics Windows Installer

This folder contains an Inno Setup script (`VideoForensics.iss`) that builds a single Windows installer
(`VideoForensicsSetup.exe`) for VideoForensics.

## Why Inno Setup (not WiX)

This installer previously used WiX v5 (an MSI + Burn bootstrapper). It was replaced because WiX v5's
current steward, FireGiant, charges an "Open Source Maintenance Fee" to commercial users above a
revenue threshold — and since VideoForensics is proprietary (not open source), it likely doesn't
qualify for the free carve-out. Inno Setup is free for any use, including closed-source commercial
software, with no revenue-based fee, ever.

The trade-off: Inno Setup does not produce a native `.msi`, so native Group Policy "Software
Installation" push deployment is not available. Silent/unattended installs via command-line switches
(see below) still work fine for scripted deployment or RMM tools.

## Prerequisites

### Build Environment

- **Inno Setup 6** — [jrsoftware.org](https://jrsoftware.org/isdl.php), or via `winget install JRSoftware.InnoSetup`. GitHub's `windows-latest` Actions runners already have it preinstalled.
- **.NET 10 SDK** (to publish `VideoForensics.WebApp` and `VideoForensics.MauiApp`)

### Target Machine (Runtime)

- **Windows 10 or later** (or Windows Server equivalents)
- **.NET 10 runtime** is NOT required — both publish outputs are self-contained

## Components

The installer offers two independently selectable components — you can install the Server only, the
Desktop client only, or both:

- **Server** (`VideoForensics.WebApp`) — installed as the `VideoForensics` Windows Service (LocalSystem,
  auto-start). Has an optional **Install bundled FFmpeg** task (checked by default) for video
  transcoding/metadata — uncheck it if you already have a system-wide ffmpeg you'd rather use (see
  [Using a Custom ffmpeg Build](#using-a-custom-ffmpeg-build) below).
- **Desktop Client** (`VideoForensics.MauiApp`) — a plain, unpackaged desktop app (no MSIX, no separate
  service) that connects to a Server over the network. Has an optional **Create Desktop & Start Menu
  shortcuts** task (checked by default).

Both components default to checked (a full install); either can be unchecked independently.

## Publishing the Application

Before building the installer, publish both self-contained components:

```bash
dotnet publish src/client/web/VideoForensics.WebApp -c Release -r win-x64 --self-contained true -o publish\server
dotnet publish src/client/maui/VideoForensics.MauiApp -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -o publish\desktop
```

## FFmpeg Binaries

VideoForensics bundles ffmpeg and ffprobe binaries with the Windows installer — no separate ffmpeg
installation needed on the target machine, as long as the **Install bundled FFmpeg** task is checked.

### What's Bundled

Statically-linked LGPL ffmpeg binaries from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds):
- `ffmpeg.exe` — video transcoding and metadata extraction
- `ffprobe.exe` — video metadata probing

Both are installed to `%ProgramFiles%\VideoForensics\` alongside `ffmpeg-LICENSE.txt` (the bundled
binaries' LGPL license/copyright notice — do not remove this file).

### Using a Custom ffmpeg Build

To use a different or newer ffmpeg version:

- **Uniview provider:** Supports a custom path via the `UniviewFfmpegPath` configuration setting, configurable through the admin UI or directly in the database.
- **Ring provider:** No user-facing configuration setting is available today. The Ring provider resolves ffmpeg from: (1) bundled binaries in the installation directory, or (2) system PATH. To use a custom ffmpeg build with Ring, you must rebuild the application with a code change.

## Hardware-Accelerated Video Decoding

The app auto-detects and uses hardware-accelerated video decoding when available, including NVIDIA
CUDA, Intel Quick Sync, and Direct3D11/DXVA2 acceleration — no separate installation or configuration
required either way; the app transparently falls back to software decoding if no compatible
hardware/driver is found.

## Building the Installer

From the repository root, after publishing both components (see above):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" deploy\windows\VideoForensics.iss `
  /DAppVersion=1.2.3 `
  /DServerPublishDir="$(Resolve-Path publish\server)" `
  /DDesktopPublishDir="$(Resolve-Path publish\desktop)"
```

The output is `deploy\windows\bin\VideoForensicsSetup.exe`.

`AppVersion`/`ServerPublishDir`/`DesktopPublishDir` all have local-dev fallback defaults in the script,
so it can also be compiled directly from a checkout for quick iteration without passing any `/D` flags
(assuming `publish\server`/`publish\desktop` exist relative to the repo root).

### CI / Release Pipeline

`.github/workflows/release-installers.yml`, `publish-testing.yml`, and `publish-dev.yml` all publish
both components, then run the same `ISCC.exe` command shown above, using the version computed by
Nerdbank.GitVersioning (`nbgv get-version -v Version`).

## Installation

### End-User Installation

Run `VideoForensicsSetup.exe`. This launches a standard installer with:
- License page
- Install location picker (defaults to `%ProgramFiles%\VideoForensics\`)
- Component/task checkboxes (Server + bundled FFmpeg, Desktop client + shortcuts)
- **Data Directory page** (Server only, fresh installs only — see below)
- Progress and completion screens

On a genuinely fresh install (not an upgrade) with the Server component selected, the installer opens
your browser to `http://localhost:5162` after the service starts, so you land directly in the app,
where the first-run `/setup` page lets you create your own administrator account.

The installer will, for the Server component:
1. Copy application binaries to `%ProgramFiles%\VideoForensics\`
2. Write the custom data directory (if one was entered) to the registry — see below
3. Register the `VideoForensics` Windows Service (LocalSystem, auto-start, restart-on-failure)
4. Create the Event Log source `VideoForensics` (Application log)
5. Start the service

### Custom Data Directory

By default, the database, media, temporary downloads, logs, reports, and backups all live under
`%ProgramData%\VideoForensics\` (encryption keys always do too, and are not user-configurable here —
see below). On a fresh install, the installer's "Data Directory" page shows one field per category,
each pre-filled with its actual resolved default path — leave a field as-is to keep that default, or
change only the ones you specifically need on a different drive.

This is only offered on a fresh install, not an upgrade: changing it later would silently orphan
data already sitting at the old location rather than moving it. To relocate data on an existing
install, use the in-app Storage Settings page (`/settings/storage`, SuperAdmin-only) instead, which
physically moves the files.

Under the hood, each edited field is stored as its own value under the registry key
`HKLM\SOFTWARE\VideoForensics` (`DatabasePath`, `MediaPath`, `TempDownloadPath`, `LogsPath`,
`ReportsPath`, `BackupPath` — all plain `REG_SZ` values, matching how the in-app Storage Settings
feature already treats these as independently relocatable) — this is read by
`StorageLocationProvider.GetDefaultRoot()` before the database opens, so it works for the Windows
Service and any command-line tool alike, regardless of how each is launched (unlike an environment
variable, which doesn't reliably propagate to an already-running service or session). Only a field
you actually edit gets a registry entry — an untouched box changes nothing. Encryption keys have no
override here on purpose: they're excluded from relocation in the in-app Storage Settings feature
too, and are too security-sensitive to expose in an installer UI. On Debian/Ubuntu, the equivalent
mechanism is a set of environment variables (`VIDEOFORENSICS_DATABASE_PATH`,
`VIDEOFORENSICS_MEDIA_PATH`, etc.), set via the systemd unit or `.deb` postinst script.

To provision a database at a custom location ahead of first service start (e.g. scripted
deployments), use the `VideoForensics.DbSetup` command-line tool bundled with the Server component,
installed to its own `Tools` subfolder (kept separate from the server's own files on purpose — see
the comment in `VideoForensics.iss` for why):

```powershell
"C:\Program Files\VideoForensics\Tools\VideoForensics.DbSetup.exe" --data-root "D:\VFData"
# or, for an exact database file path:
"C:\Program Files\VideoForensics\Tools\VideoForensics.DbSetup.exe" --db-path "D:\VFData\videoforensics.db"
```

With no arguments, it creates/migrates the database at whatever path the server itself would
currently use (respecting the registry override above, if one is set).

### Silent / Unattended Installation

```powershell
VideoForensicsSetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

Inno Setup's standard command-line switches apply (`/VERYSILENT`, `/SILENT`, `/DIR="..."`,
`/COMPONENTS="server,desktop"`, `/LOG="install.log"`, etc.) — see [Inno Setup's command-line
documentation](https://jrsoftware.org/ishelp/index.php?topic=setupcmdline) for the full list.

## Upgrade

To upgrade to a newer version, run the new `VideoForensicsSetup.exe` — Inno Setup detects the existing
install (same fixed `AppId`) and installs on top of it automatically:

1. Detects the existing installation
2. Stops the running `VideoForensics` service (so files aren't locked during copy)
3. Replaces binaries in `%ProgramFiles%\VideoForensics\`
4. Restarts the service

The database, encryption keys, and all user data in `%ProgramData%\VideoForensics\` are preserved and
automatically migrated (if needed) when the service restarts — the app's `DatabaseInitializer` handles
schema migrations on startup, not the installer. Service registration steps (`sc create`, failure
recovery config) are skipped on upgrade since the service is already registered — only `sc start` runs.

**No manual uninstall or data migration is required** — the upgrade is atomic and safe.

## Uninstallation

### Remove (Keep Data)

Via Control Panel:
1. Settings → Apps → Apps & features
2. Search for "VideoForensics"
3. Click "Uninstall"

This will:
1. Stop the VideoForensics Windows Service
2. Remove the service registration (`sc delete`) and the Event Log source registry key
3. Remove binaries from `%ProgramFiles%\VideoForensics\`
4. **Leave all data in `%ProgramData%\VideoForensics\` intact**

The data directory contains the SQLite database, encryption keys, and logs — it is safe to keep after
uninstalling if you plan to reinstall later.

### Complete Removal (Delete Data)

To completely remove all traces of VideoForensics (including its database):

1. Uninstall via Control Panel (see above)
2. Manually delete the data directory:
   ```powershell
   Remove-Item -Recurse -Force $env:ProgramData\VideoForensics
   ```

**Warning:** This is irreversible. Only delete the data directory if you are certain you want to lose
all application data (database, encryption keys, settings).

## Signing Status

**`VideoForensicsSetup.exe` is UNSIGNED.**

This means:
- On Windows 10+ with SmartScreen enabled, you may see warnings: "Windows protected your PC" / "unknown publisher"
- To dismiss this warning and run the installer:
  1. Click "More info" on the SmartScreen prompt
  2. Click "Run anyway"
- Or, disable SmartScreen (not recommended for production):
  - Settings → Update & Security → Windows Security → App & browser control → Reputation-based protection (toggle off)

**Signing Support Coming Later:** Code signing requires a code-signing certificate. Once a certificate
is available, signing will be added as a CI pipeline step (sign after build, before release) — this
does not require changes to the Inno Setup script.

## Service Configuration

| Property | Value |
|----------|-------|
| **Service Name** | `VideoForensics` |
| **Display Name** | `VideoForensics` |
| **Startup Type** | Automatic |
| **Run As** | Local System |
| **Failure Recovery** | Restart after 5 seconds, up to 3 times, within a 24-hour reset window |
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
| **Install Directory** | `%ProgramFiles%\VideoForensics\` (server binaries), `%ProgramFiles%\VideoForensics\Desktop\` (desktop client) |
| **Database** | `%ProgramData%\VideoForensics\VideoForensics.db` |
| **Encryption Keys** | `%ProgramData%\VideoForensics\` (DPAPI-protected) |
| **Logs** | `%ProgramData%\VideoForensics\logs\` (if file logging enabled) |
| **Configuration** | `%ProgramData%\VideoForensics\appsettings.json` (optional overrides) |

The data directory is **shared** across all hosting contexts (service, interactive, debug) and is never
touched by the installer or uninstaller.

## LAN Discovery (mDNS) — No Bonjour Required

VideoForensics advertises the Server on the LAN via mDNS/DNS-SD so the Desktop client (and mobile
pairing) can find it without typing an IP address. This uses the [`Makaretu.Dns`](https://github.com/richardschneider/net-mdns)
managed .NET library, which implements mDNS itself over raw UDP multicast — it does **not** depend on
or require Apple's Bonjour/`mDNSResponder` service (which Windows lacks natively). Nothing extra needs
to be installed for LAN discovery to work; it's a plain NuGet dependency already bundled with the app.

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

This is expected because the executable is unsigned. See [Signing Status](#signing-status) above for
how to dismiss the warning.

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

## Keeping vs. Retiring PowerShell Scripts

`deploy/install-service.ps1` and `deploy/uninstall-service.ps1` are still available and unchanged. Use
the Inno Setup installer for end-user distribution, but keep the PowerShell scripts for:
- Developer/CI quick-iteration (no installer compile time)
- Testing service registration logic independently
- Educational reference (showing the underlying Windows Service setup patterns)

## Hot-Swapping the Service During Development

`deploy/windows/hotswap-service.ps1` updates an **already-installed** service's binaries without a
full installer round-trip — no Inno Setup compile, no re-registering the service, no UAC prompt
beyond the script's own elevation. It stops the service, copies over only newer files (never
deletes anything, so it can't accidentally remove unrelated installed content), and restarts it,
then checks `http://localhost:5162` to confirm the new build actually came up.

```powershell
# Publish current source and swap it in, in one step
.\deploy\windows\hotswap-service.ps1 -Build

# Swap in an already-published output without rebuilding
.\deploy\windows\hotswap-service.ps1 -PublishDir C:\Build\VideoForensics
```

**Data safety:** this script only ever touches `%ProgramFiles%\VideoForensics` (binaries). It never
references `%ProgramData%\VideoForensics` (database, media, logs, keys) in any way — do not add such
a reference to it. Requires the service to already be installed (run the Inno Setup installer once
first); this is purely a fast-iteration tool for after that.

## Important Notes

### Windows Service Considerations

- Installing a Windows Service is a **system-level change** that persists across reboots
- The service runs in the background regardless of user login
- This is intentional for production deployments; do not run the installer as part of routine dev loops
- Use PowerShell scripts for development; save the installer for deployment

### Self-Contained Delivery

The installer packages self-contained .NET 10 binaries, so the target machine does not need:
- .NET 10 runtime pre-installed
- Visual C++ redistributables (unless VC++-specific libraries are added to the app in the future)

This simplifies deployment but makes the installer larger (~150-200 MB) than a framework-dependent build.

### No Auto-Update

The installer supports re-running to upgrade, but does not implement in-app "check for updates" or
silent auto-update beyond the update-check feature already built into the app itself (see the root
README's "Release Channels" section). Users must download a new installer manually (or via your
release distribution channel) unless the app's own update-check triggers the download.
