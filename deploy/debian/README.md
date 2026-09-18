# VideoForensics Debian Package

This folder contains packaging metadata and scripts to build and install VideoForensics as a Debian (.deb) package for Ubuntu and Debian-based Linux distributions.

## Prerequisites

- **Debian 11+ or Ubuntu 20.04 LTS+** (target distros with modern glibc and libicu support)
- **Administrator/root privileges** (installation, service management)
- **Self-contained publish:** `.deb` ships a self-contained .NET 10 linux-x64 publish, so no .NET runtime is required on the target machine

## Building the Package

A Bash build script is provided to assemble the `.deb` file from a self-contained publish output.

### Quick Build

Publish the WebApp self-contained:

```bash
dotnet publish src/client/web/VideoForensics.WebApp -c Release -r linux-x64 --self-contained true -o /tmp/VideoForensics.publish
```

Build the `.deb`:

```bash
./deploy/debian/build-deb.sh /tmp/VideoForensics.publish 1.0.0
```

This produces `videoforensics_1.0.0_amd64.deb` in the script's working directory.

### Build Script Options

```bash
./deploy/debian/build-deb.sh [published-path] [version]
```

- `published-path` (optional): path to the self-contained publish output (default: `../src/client/web/VideoForensics.WebApp/bin/Release/net10.0/linux-x64/publish`)
- `version` (optional): version string for the package (default: "1.0.0" or read from `$VERSION` env var)

Examples:

```bash
# Use defaults
./deploy/debian/build-deb.sh

# Custom publish path and version
./deploy/debian/build-deb.sh /opt/build/publish 2.0.1

# Version from environment variable
VERSION=2.0.0 ./deploy/debian/build-deb.sh /opt/build/publish
```

## Installation

### Fresh Install

```bash
# Single installation
sudo dpkg -i videoforensics_1.0.0_amd64.deb

# Or via apt (requires .deb file locally)
sudo apt install ./videoforensics_1.0.0_amd64.deb
```

The installation script (`postinst`) will:
1. Create a dedicated `videoforensics` system user and group
2. Create the data directory `/var/lib/videoforensics` with proper ownership
3. Register and enable the systemd service
4. Start the service

### Upgrade

To upgrade to a newer version, simply re-run the install command with the new `.deb` file:

```bash
sudo dpkg -i videoforensics_2.0.0_amd64.deb
# or
sudo apt install ./videoforensics_2.0.0_amd64.deb
```

The `postinst` script detects the upgrade and:
1. Stops the running service
2. Replaces the binary files
3. Restarts the service
4. **Does NOT touch user data** — the database and configuration in `/var/lib/videoforensics/` are preserved
5. **Does NOT require a manual migration step** — the app runs `DatabaseInitializer.InitializeAsync` on startup, which handles all schema updates automatically

### Downgrade (not recommended)

Downgrading is supported but not recommended, especially if the newer version introduced database migrations. To downgrade:

```bash
sudo apt install ./videoforensics_1.9.0_amd64.deb --allow-downgrades
```

## Removal

### Remove (keep data)

```bash
sudo dpkg -r videoforensics
# or
sudo apt remove videoforensics
```

This will:
1. Stop the service
2. Remove the service registration
3. **Leave the data directory `/var/lib/videoforensics/` intact** (contains database, encryption keys, logs)

The service and binaries are removed, but if you reinstall later, your data will be preserved.

### Purge (complete removal)

To remove everything including user data:

```bash
sudo dpkg -P videoforensics
# or
sudo apt purge videoforensics
```

This will:
1. Stop the service
2. Remove the service registration
3. **Delete the data directory `/var/lib/videoforensics/`** (this contains the database and is not recoverable)
4. Remove the `videoforensics` system user

**Warning:** purging is irreversible. Use only if you are certain you want to delete all application data.

## Data Directory

All persistent application data is stored in `/var/lib/videoforensics/`:

- **Database:** `VideoForensics.db` (SQLite)
- **Encryption keys:** DPAPI-equivalent keys for credential storage
- **Configuration:** Application settings and cache
- **Logs:** Diagnostic logs (if file logging is configured)

This directory is:
- **Created on fresh install** by `postinst` with ownership `videoforensics:videoforensics`
- **Preserved on upgrade** — `postinst` never modifies its permissions or content during an upgrade
- **Preserved on remove** — only `dpkg -P` (purge) deletes it, not plain `apt remove`
- **Deleted only on purge** — this is an explicit user action to completely remove the application

## Service Details

| Property | Value |
|----------|-------|
| **Service Name** | `videoforensics` |
| **Systemd Unit** | `/lib/systemd/system/videoforensics.service` |
| **Run As** | Dedicated non-root system user `videoforensics` |
| **Binaries Location** | `/usr/lib/videoforensics/` |
| **Data Location** | `/var/lib/videoforensics/` |
| **Start Type** | Enabled on boot via `systemctl enable` |
| **Restart Policy** | Automatic restart on failure (60-second delay) |
| **Logging** | systemd journal (`journalctl -u videoforensics`) |

## Viewing Logs

Monitor the service logs in real time:

```bash
sudo journalctl -u videoforensics -f
```

View recent logs:

```bash
sudo journalctl -u videoforensics -n 50
```

View logs since boot:

```bash
sudo journalctl -u videoforensics -b
```

## Service Management

### Manual Start/Stop

```bash
# Start the service
sudo systemctl start videoforensics

# Stop the service
sudo systemctl stop videoforensics

# Restart the service
sudo systemctl restart videoforensics

# Check service status
sudo systemctl status videoforensics
```

### Enable/Disable on Boot

```bash
# Enable automatic startup on boot
sudo systemctl enable videoforensics

# Disable automatic startup on boot
sudo systemctl disable videoforensics

# Check if enabled
sudo systemctl is-enabled videoforensics
```

## Troubleshooting

**Service fails to start:**
- Check logs: `sudo journalctl -u videoforensics -n 20`
- Verify binaries exist: `ls -la /usr/lib/videoforensics/`
- Check ownership: `ls -la /var/lib/videoforensics/`

**Permission denied errors:**
- Ensure the `videoforensics` user can write to `/var/lib/videoforensics/`:
  ```bash
  sudo chown -R videoforensics:videoforensics /var/lib/videoforensics
  sudo chmod -R 0750 /var/lib/videoforensics
  ```

**Can't connect to the application:**
- Check if the service is running: `sudo systemctl status videoforensics`
- Check which port it's listening on (default: check `appsettings.json` in `/usr/lib/videoforensics/`)
- Verify firewall rules allow access

**Need to completely uninstall:**
1. Purge the package: `sudo apt purge videoforensics`
2. Verify data directory is gone: `sudo ls -la /var/lib/videoforensics` (should not exist)
3. Verify system user is gone: `id videoforensics` (should show "no such user")

## Important Notes

### No signed packages (unsigned for now)

This `.deb` is **unsigned**. There is currently no code-signing certificate in use. If your system requires signed packages, you will need to build your own signing infrastructure or add the package to a trusted source manually.

### No hosted apt repository (yet)

VideoForensics does not yet have a hosted apt repository or PPA. Distribution is via direct `.deb` file download (GitHub releases, etc.). To upgrade, download the new `.deb` and re-run the install command. A hosted apt repository with auto-update support may be added in the future.

### Database migrations are automatic

When upgrading, the application automatically migrates its SQLite database schema on startup. No manual `dotnet ef database update` or similar command is needed — the `postinst` script simply restarts the service and the app handles the rest.

### Mirrors Windows uninstall behavior

The data-preservation strategy mirrors the existing Windows installer (`deploy/install-service.ps1` / `uninstall-service.ps1`):
- `remove`: keeps binaries and data (safe for reinstall)
- purge: only removes everything when explicitly requested

This ensures users can safely remove the package without losing their data unless they explicitly ask for complete removal.
