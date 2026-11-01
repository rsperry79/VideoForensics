# Building VideoForensics

Developer setup for building and running from source. Run every command from the repository root, where `VideoForensics.sln` lives, unless a step says otherwise.

## Prerequisites

- .NET SDK 10.0 (the repo pins the SDK in `global.json`).
- Node.js for the JavaScript gate (`npm ci`, `npm run check`). Use the version the repo's `package.json` expects.
- Windows 11 for the MAUI and Windows Service targets. Debian/Ubuntu for the Linux package.

## Syncfusion license key

The UI uses Syncfusion Blazor, which needs a license key. Without one, a local build runs in unlicensed dev mode and shows the Syncfusion trial banner.

Local builds read the key from .NET user secrets. Do not put it in a file under `src/`, and do not set an environment variable.

Run this once, from the Client.Core source folder:

```bash
cd src/client/core/VideoForensics.Client.Core/src
dotnet user-secrets set SYNCFUSION_LICENSE_KEY "<your-key>"
```

Ask a maintainer for the key. It is the same value as the `SYNCFUSION_LICENSE_KEY` GitHub Actions secret, which CI uses for release builds.

How the key reaches each build:

| Build | Source of the key |
| --- | --- |
| Local (`dotnet build`, Visual Studio) | User secrets, as above |
| CI (`build-windows.yml`) | The `SYNCFUSION_LICENSE_KEY` secret, written to `src/client/web/VideoForensics.WebApp/src/syncfusion-license.key` and embedded in the WebApp as the `syncfusion-license.key` resource |

The embedded file is git-ignored. Never commit it, and never paste the key into code, config, or chat.

Key lookup order at startup: the embedded resource, then user secrets, then the key baked into `VideoForensics.Client.Core.dll` at CI build time. The lookup logic is in `SyncfusionLicenseRegistrar`.

## Build and test

```bash
dotnet restore VideoForensics.sln
dotnet build VideoForensics.sln
dotnet test VideoForensics.sln
```

For focused iteration, run only the tests you touched:

```bash
dotnet test --filter "FullyQualifiedName~SyncfusionLicenseRegistrarTests"
```

The full pre-PR gate is in the [Testing section of the README](README.md#testing).
