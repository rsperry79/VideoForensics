---
paths:
  - "src/client/**/*.csproj"
  - "src/client/**/Program.cs"
  - ".github/workflows/**"
---

# Syncfusion license

- One resolver: `SyncfusionLicenseRegistrar` in `VideoForensics.Client.Core`. Do not add another license reader.
- Priority: embedded `syncfusion-license.key` resource in the WebApp > user secrets > key baked into `VideoForensics.Client.Core.dll` at CI build time.
- CI: `build-windows.yml` writes `src/client/web/VideoForensics.WebApp/src/syncfusion-license.key` from the `SYNCFUSION_LICENSE_KEY` secret (git-ignored, embedded by the WebApp csproj), and passes it as `-p:SyncfusionLicenseKey`.
- Developers: `dotnet user-secrets set SYNCFUSION_LICENSE_KEY <key>` in `src/client/core/VideoForensics.Client.Core/src`. No env var, no key file. Unlicensed without a key.
