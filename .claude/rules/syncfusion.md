---
paths:
  - "src/client/**/*.csproj"
  - "src/client/**/Program.cs"
  - ".github/workflows/**"
---

# Syncfusion license

- One resolver: `SyncfusionLicenseRegistrar` in `VideoForensics.Client.Core`. Do not add another license reader.
- Priority: `%ProgramData%\VideoForensics\syncfusion-license.key` > user secrets > key baked into `VideoForensics.Client.Core.dll` at CI build time.
- CI: `SYNCFUSION_LICENSE_KEY` secret passed as `-p:SyncfusionLicenseKey` in `release-installers.yml` and `build-windows.yml`.
- Developers: `dotnet user-secrets set SYNCFUSION_LICENSE_KEY <key>` in `src/client/core/VideoForensics.Client.Core/src`. No env var. Unlicensed without a key.
