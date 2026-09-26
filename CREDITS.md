# Third-Party Credits

This repository depends on and/or bundles the following third-party components. The licenses listed here do NOT extend to this repository's own code, which is MIT licensed—see the LICENSE file for the terms governing this project.

## Syncfusion Essential Studio

**License:** Commercial (Paid License / Community License)

This repository's UI components (`VideoForensics.WebApp`, `VideoForensics.MauiApp`) use Syncfusion Blazor and MAUI components. A valid Syncfusion license is required to build and use this software. The license key is registered at runtime via `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(...)` (read from a configuration file, not embedded in source). Developers must obtain their own valid license from https://www.syncfusion.com/sales/teamlicense or qualify for Syncfusion's free Community License based on team size and revenue criteria.

## ffmpeg

**License:** LGPL

A prebuilt ffmpeg/ffprobe binary is bundled into the Windows and Debian installers for video processing and analysis. See `.github/workflows/release-installers.yml` for build and distribution details.

## cloudflared

**License:** Apache 2.0

A prebuilt cloudflared binary is bundled into the installers for optional remote-access tunneling. See `src/client/host/VideoForensics.Hosting/CloudflaredTunnelService.cs` for integration details.

## Inno Setup

**License:** ISPL (Inno Setup License, free for any use including closed-source commercial software)

Used at build time (not bundled or distributed with the final software) to produce the Windows installer. See `deploy/windows/VideoForensics.iss` for build configuration.

## Ring.Api

**License:** MIT

This repository originated as a fork and derivative of the open-source Ring.Api project (https://github.com/Ring/Ring.Api) for communicating with the Ring camera API. Credit to the original authors and contributors of that project.
