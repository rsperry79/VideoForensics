# VideoForensics

[![CI](https://github.com/rsperry79/VideoForensics/actions/workflows/ci.yml/badge.svg)](https://github.com/rsperry79/VideoForensics/actions/workflows/ci.yml)

VideoForensics is a forensic video evidence capture and management platform that aggregates video recordings and device information from multiple camera and doorbell providers (Ring, Wyze, Uniview) into a centralized, chain-of-custody system for evidence analysis and reporting.

## Installation

The recommended way to install VideoForensics is via one of the thin bootstrap scripts below, which fetch the current release for your chosen channel at runtime:

```bash
# Windows
irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 | iex

# Windows (Testing channel — rolling prerelease from the dev branch)
&([scriptblock]::Create((irm https://raw.githubusercontent.com/rsperry79/VideoForensics/dev/deploy/install.ps1))) -Channel Testing

# Debian/Ubuntu
curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash

# Debian/Ubuntu (Testing channel)
curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/dev/deploy/install.sh | sudo bash -- --channel testing
```

Alternatively, direct installer downloads (`VideoForensicsSetup.exe` for Windows, or a Debian `.deb` package) are available from the [GitHub Releases](https://github.com/rsperry79/VideoForensics/releases) page for air-gapped environments or manual deployment. See [`deploy/README.md`](deploy/README.md) for detailed installation documentation and configuration options.

## Release Channels

VideoForensics publishes two release channels:

- **Stable**: Tagged releases (`vX.Y.Z`) off the `main` branch, published as GitHub Releases. Assets are signed and ready for production use.
- **Testing**: Rolling prerelease built on every push to the `dev` branch and tagged as `testing`. This channel receives new features first and is suitable for testing, but may be unstable.

Feature work lands via pull request onto `wip`, then `wip` is periodically PR'd into `dev` for integration testing, and `dev` is PR'd into `main` for release.

Both channels are configurable per-installation via the built-in update-check feature, which can notify you of available updates or automatically download and install them.

## Components

- **VideoForensics.WebApp** — ASP.NET Core Blazor Server UI and REST API backend. Runs as a Windows Service (Windows) or systemd service (Debian/Ubuntu). Manages all forensic data, evidence chain-of-custody, device connectivity, and multi-user access control.
- **VideoForensics.MauiApp** — .NET MAUI desktop client for Windows, with iOS support via local developer sideload (see [`deploy/ios/README.md`](deploy/ios/README.md) for details). Connects to the WebApp server over the network.
- **VideoForensics.Client.MCP** — Model Context Protocol (MCP) server for AI-assistant integration with Claude and other MCP-compatible tools.

## Testing

Run the full gate from the repository root before opening a PR to `dev` or `main`:

```bash
# .NET: clean, build, then the whole test suite
dotnet clean VideoForensics.sln
dotnet build VideoForensics.sln
dotnet test VideoForensics.sln

# JavaScript: TypeScript compile, lint and Vitest
npm ci
npm run check
```

For local iteration, run only the tests you touched with `dotnet test --filter`, for example `--filter "FullyQualifiedName~QueryApi"`.

### Skipped tests

Some tests skip instead of failing. They call real services, so they need credentials or hardware that CI and most developer machines don't have.

- **Live Ring tests** (`RealSnapshotIntegrationTests`, `RealTimeLapseIntegrationTests`, `RealLiveViewIntegrationTests`, `RealIntegrationTests`, `RealAuthStatusIntegrationTests`, and the live parts of `UnitTest`) skip when no Ring credentials are saved in the database. Ring requires a two-factor code to sign in, and the saved refresh token is encrypted with a key that only exists on the machine that created it, so CI cannot restore it. To run them, sign in once with `dotnet run -- --auth` from `src/selftest`, then rerun the tests.
- **Live Anthropic test** skips when `ANTHROPIC_API_KEY` is empty, so forks without the repository secret stay green.
- **Time-lapse tests** skip when `ffprobe` is not installed or on `PATH`. Install ffmpeg to run them.
- **Doorbot-specific tests** skip when the doorbot named in `RING_TEST_DOORBOT` is not on the account.

A skipped test is not a pass. Before a release, run the live tests locally against a real account and confirm they run rather than skip.

## License

This software is licensed under the [MIT License](LICENSE). Third-party dependencies — including Syncfusion Essential Studio, which is proprietary and commercially licensed — are not covered by this license; see the [`LICENSE`](LICENSE) file and [`CREDITS.md`](CREDITS.md) for details.

## Changelog

Project changes are documented in [`CHANGELOG.md`](CHANGELOG.md), which adheres to [Keep a Changelog](https://keepachangelog.com/) conventions.
