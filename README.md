# VideoForensics

[![CI](https://github.com/rsperry79/VideoForensics/actions/workflows/ci.yml/badge.svg)](https://github.com/rsperry79/VideoForensics/actions/workflows/ci.yml)

VideoForensics is a forensic video evidence capture and management platform that aggregates video recordings and device information from multiple camera and doorbell providers (Ring, Wyze, Uniview) into a centralized, chain-of-custody system for evidence analysis and reporting.

## Installation

The recommended way to install VideoForensics is via one of the thin bootstrap scripts below, which fetch the current release for your chosen channel at runtime:

```bash
# Windows
irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 | iex

# Debian/Ubuntu
curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash
```

Alternatively, direct installer downloads (Windows MSI + Burn bootstrapper, or Debian `.deb` package) are available from the [GitHub Releases](https://github.com/rsperry79/VideoForensics/releases) page for air-gapped environments or manual deployment. See [`deploy/README.md`](deploy/README.md) for detailed installation documentation and configuration options.

## Release Channels

VideoForensics publishes two release channels:

- **Stable**: Tagged releases (`vX.Y.Z`) off the `main` branch, published as GitHub Releases. Assets are signed and ready for production use.
- **Testing**: Rolling prerelease built on every push to the `dev` branch and tagged as `testing`. This channel receives new features first and is suitable for testing, but may be unstable.

Feature work lands via pull request onto `wip`, then `wip` is periodically PR'd into `dev` for integration testing, and `dev` is PR'd into `main` for release.

Both channels are configurable per-installation via the built-in update-check feature, which can notify you of available updates or automatically download and install them.

## Components

- **VideoForensics.WebApp** — ASP.NET Core Blazor Server UI and REST API backend. Runs as a Windows Service (Windows) or systemd service (Debian/Ubuntu). Manages all forensic data, evidence chain-of-custody, device connectivity, and multi-user access control.
- **VideoForensics.MauiApp** — .NET MAUI desktop client for Windows. Connects to the WebApp server over the network.
- **VideoForensics.Mcp** — Model Context Protocol (MCP) server for AI-assistant integration with Claude and other MCP-compatible tools.

## License

This software is proprietary and all rights are reserved. See the [`LICENSE`](LICENSE) file for details. Third-party component licenses are listed in [`CREDITS.md`](CREDITS.md).

## Changelog

Project changes are documented in [`CHANGELOG.md`](CHANGELOG.md), which adheres to [Keep a Changelog](https://keepachangelog.com/) conventions.
