# Windows Installer Overhaul: Inno Setup + First-Run Setup + Configurable Storage Paths

## Context

This started as "fix the docs for the testing-channel installer" and grew through the conversation into four related asks once the current WiX-based Windows installer was actually run and inspected:

1. The bootstrapper you ran only offered a bare "Install" button — no path selection, no component choice. That's because `Bundle.wxs` uses WiX's minimal `hyperlinkLicense` theme, which has no directory picker or feature tree built in.
2. Researching how to add that surfaced a real licensing risk: WiX v5 (now stewarded by FireGiant) charges an "Open Source Maintenance Fee" to commercial users above a revenue threshold, and VideoForensics is proprietary (not OSS), so it likely doesn't qualify for the free carve-out. You chose to replace WiX entirely with **Inno Setup**, which is free for any use, forever, with no revenue-based fee.
3. You then asked for a fuller installer: independent checkboxes for Server vs. Desktop client, shortcuts, and bundled FFmpeg, plus configurable install/data/media/DB paths, plus a proper first-run admin setup wizard instead of the fixed `admin`/`ChangeMe123!` seed.
4. Investigating the storage-path request surfaced a second real finding: the app has **no working mechanism today** to override the data directory via config — `StorageLocationProvider.GetDefaultRoot()` hardcodes `%ProgramData%\VideoForensics`, and a related bug in `ConfigurationLoader.LoadAndApplyAsync` silently drops 4 of 6 location settings when loading persisted config. An installer-level "choose your data path" field would be a no-op without an app-code fix.

Everything below was verified by direct file reads during planning, not just subagent summaries — `StorageLocationProvider.cs`, `ConfigurationLoader.cs`, `AuthGate.razor`, `VideoForensicsHostingExtensions.cs`'s seeding block, and `AddVideoForensicsSqlite` were all read directly and match the design below.

Intended outcome: replace the WiX installer with an Inno Setup one that can install the Server and/or Desktop client with a real path picker, add a password-based first-run setup wizard for the initial SuperAdmin account, and make the data/media/DB root actually configurable at install time — while keeping every existing data-safety guarantee (`%ProgramData%\VideoForensics` is never touched by upgrade/uninstall) intact.

## Phasing

Four phases, each independently shippable and testable, in this order:

- **Phase A — Inno Setup installer** (install-path + component selection only; no data-path UI yet). Self-contained deploy tooling, no app-code dependency, removes the WiX licensing exposure fastest.
- **Phase B — First-run setup wizard** (`/setup`). Isolated to `VideoForensics.WebApp`/`VideoForensics.Ui.Shared`/`VideoForensics.Hosting`. No dependency on A or C; safe to build in parallel, sequenced second to keep PRs small.
- **Phase C — Data-root override mechanism + `ConfigurationLoader` bug fix**. The riskiest change (startup sequencing across 3 hosts). Done last, once A and B have already validated the new install/uninstall and first-run flows.
- **Phase D — Integration**: wire Phase C's override into the Phase A installer's UI as a real "Data Directory" picker, final documentation sweep.

Each phase gets its own PR through the lite/full gate process already established in this repo.

---

## Phase A — Inno Setup Installer

### New/removed files

Remove entirely: `deploy/windows/VideoForensics.Bootstrapper.Wix/` and `deploy/windows/VideoForensics.Installer.Wix/` (both `.wxs`/`.wixproj`).

Add: `deploy/windows/VideoForensics.iss` (single Inno Setup script — `[Setup]`, `[Components]`, `[Tasks]`, `[Files]`, `[Icons]`, `[Run]`, `[UninstallRun]`, `[Code]`). Keep everything in one file; only extract a helper `.ps1` later if Pascal Script becomes unwieldy.

### Components/Tasks (no custom wizard page needed for this part)

```
[Components]
Name: "server"; Description: "VideoForensics Server (Windows Service)"; Types: full custom
Name: "desktop"; Description: "Desktop Client"; Types: full custom; Flags: unchecked

[Tasks]
Name: "ffmpeg"; Description: "Install bundled FFmpeg"; Components: server; Flags: checkedonce
Name: "shortcuts"; Description: "Create Desktop && Start Menu shortcuts"; Components: desktop; Flags: checkedonce
```
`Components: server`/`desktop` on the `[Tasks]` lines automatically hides/greys the FFmpeg and shortcut checkboxes unless their parent component is selected. Server defaults checked, Desktop defaults unchecked (`Flags: unchecked`).

### Service lifecycle (replaces WiX ServiceInstall/ServiceControl, finally implements the WiX TODOs)

`[Run]` (Components: server):
```
Filename: "sc.exe"; Parameters: "create VideoForensics binPath= ""{app}\VideoForensics.WebApp.exe"" start= auto"; Flags: runhidden
Filename: "sc.exe"; Parameters: "failure VideoForensics reset= 86400 actions= restart/5000/restart/5000/restart/5000"; Flags: runhidden
Filename: "sc.exe"; Parameters: "start VideoForensics"; Flags: runhidden
```
Event Log source registration (the other unfinished WiX TODO): in `[Code]`'s `CurStepChanged(ssPostInstall)`, `RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\EventLog\Application\VideoForensics', 'EventMessageFile', ExpandConstant('{app}\VideoForensics.WebApp.exe'))` — no PowerShell dependency needed.

`[UninstallRun]`: `sc.exe stop VideoForensics` then `sc.exe delete VideoForensics`; plus `RegDeleteKeyIncludingSubkeys` for the Event Log key in `CurUninstallStepChanged(usPostUninstall)`.

### Upgrade support and data preservation

- `[Setup]` sets a fixed, generated-once `AppId` (Inno's UpgradeCode equivalent) — never regenerate it.
- `InitializeSetup()`/`[Code]`: detect an existing install via the `AppId`'s uninstall registry key; if found, stop the service before file copy, restart after — Inno's default behavior already installs "on top of" the same `AppId`'s directory.
- Data safety: `%ProgramData%\VideoForensics\` is simply never referenced in `[Files]` and never gets an uninstall delete entry — same structural guarantee as the WiX version (absence of an instruction, not an explicit exclusion). Comment this prominently in the `.iss` file.

### CI changes

Files: `.github/workflows/release-installers.yml`, `.github/workflows/publish-testing.yml`, `.github/workflows/publish-dev.yml`.

Replace each workflow's `dotnet build deploy/windows/VideoForensics.Bootstrapper.Wix -p:PublishDir=... -p:ProductVersion=...` step with:
```yaml
- name: Publish WebApp (win-x64, self-contained)
  run: dotnet publish src/client/web/VideoForensics.WebApp -c Release -r win-x64 --self-contained true -o publish\server

- name: Publish MauiApp (win-x64, self-contained)   # new — not currently published anywhere
  run: dotnet publish src/client/maui/VideoForensics.MauiApp -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -o publish\desktop

# existing FFmpeg/cloudflared bundling steps now target publish\server

- name: Compile Inno Setup installer
  run: ISCC.exe deploy\windows\VideoForensics.iss /DAppVersion=${{ steps.extract-version.outputs.version }} /DServerPublishDir=publish\server /DDesktopPublishDir=publish\desktop
```
`windows-latest` GitHub-hosted runners ship Inno Setup 6 preinstalled (`C:\Program Files (x86)\Inno Setup 6\ISCC.exe`) — call it directly; don't add an unconditional `choco install innosetup` fallback step (slows every run).

While touching these three workflows, also fix the pre-existing version-derivation inconsistency: `publish-testing.yml` uses `dotnet msbuild -getProperty:Version` (silently wrong — NBGV computes version via an MSBuild target, not a static property) while the other two correctly use `nbgv get-version -v Version`. Standardize all three on the `nbgv` approach.

Rename the output artifact from `VideoForensicsBootstrapper.exe` to `VideoForensicsSetup.exe` (`OutputBaseFilename` in `[Setup]`) and update the upload/checksum/release-asset steps accordingly.

### Every reference to the old filename that must change together

- `deploy/install.ps1` (the `$release.assets | Where-Object { $_.name -eq "VideoForensicsBootstrapper.exe" }` match, plus the adjacent temp-path/log lines)
- `deploy/install.sh` (equivalent asset-name match, if present)
- `src/client/host/VideoForensics.Hosting.Tests/GitHubReleaseClientTests.cs` (mock release-JSON fixture hardcodes the old literal filename — pure find/replace, test data only)
- `deploy/windows/README.md` — full rewrite: Inno script layout, component/task descriptions, `AppId` upgrade behavior, the newly-implemented service failure-recovery, and an explicit note that LAN discovery uses the managed `Makaretu.Dns` library (already confirmed in this session) and needs no Bonjour/mDNSResponder install
- `README.md` — Installation section's "Windows MSI + Burn bootstrapper" line
- `deploy/README.md` — the "Bootstrap Installer Scripts" section's asset-name reference
- `CREDITS.md` — remove WiX Toolset attribution, add Inno Setup (jrsoftware.org, ISPL license)

### Verification

On the real Windows test machine already used this session: **first uninstall the existing WiX-based install** (Programs & Features → Uninstall "VideoForensics"; confirm `Get-Service VideoForensics` returns not-found) before testing the Inno build, to avoid a stale service-name collision. Then: `ISCC.exe deploy\windows\VideoForensics.iss`, run the output exe, and verify server-only / desktop-only / both-components installs, an upgrade-over-existing-install run, and a full uninstall (service gone, Event Log key gone, `%ProgramData%\VideoForensics` untouched).

---

## Phase B — First-Run Setup Wizard (`/setup`)

### New files

- `src/client/web/VideoForensics.WebApp/Api/SetupEndpoints.cs` — mirrors the structure of `PairingEndpoints.cs`. `MapSetupEndpoints(this WebApplication app)`, registered alongside the existing `MapPairingEndpoints`/`MapOperatorAuthEndpoints` calls.
  - `POST /api/v1/setup/create-admin`, gated by `await operators.IsEmptyAsync(ct)` — `Results.Forbid()` once any operator exists (same gating pattern as `PairingEndpoints.initiate`).
  - Reuses `PasswordHasher<Operator>` + `IOperatorRepository.AddAsync`/`GetByUsernameAsync`, exactly like `OperatorAuthEndpoints.RegisterAsync`, but sets `Role = SuperAdmin`, `IsApproved = true`, `MustChangePassword = false`, `Active = true` (vs. `RegisterAsync`'s always-`ReadOnly`/unapproved).
  - Logs a `SecurityAuditLogger` event analogous to `PairingEndpoints`' `PairingInitiated` logging.
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Setup.razor` — username/password/confirm form posting to the endpoint above, redirecting to sign-in on success.

### Changed files

- `AuthGate.razor` (`src/client/ui/VideoForensics.Ui.Shared/Layout/AuthGate.razor`): add `"/setup"` to `AllowListedRoutes` (line 43-49); change the bootstrap redirect target at line 120 from `/pair` to `/setup`. Leave `/pair`'s existing bootstrap branch in `PairingEndpoints.cs` as-is (don't delete it — it's still reachable by direct URL and may serve QR/mobile pairing onboarding; just no longer the default UI route for an empty DB). Flag this as a decision worth a second look in code review.
- `VideoForensicsHostingExtensions.cs` (seeding block, lines 615-641): wrap the existing `admin`/`ChangeMe123!` auto-seed behind a new env var, e.g. `VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN` (default off once `/setup` ships). When unset/false: leave the DB empty for `/setup` to handle. When explicitly `true`: keep today's exact behavior, for headless/scripted deployments that can't drive a browser wizard.
- `CLAUDE.md` (~lines 129-133, "Default SuperAdmin Account" section): document both paths — the interactive `/setup` wizard (new default) and the env-var-gated legacy seed (headless deployments).

### Verification

New test alongside whatever test file covers `OperatorAuthEndpoints.RegisterAsync`: assert `POST /api/v1/setup/create-admin` on an empty DB creates a `SuperAdmin` + `IsApproved=true` operator, and a second call (non-empty DB) returns 403. Manual: fresh/empty DB, confirm the app redirects to `/setup` (not `/pair`), complete the form, confirm login works with SuperAdmin role, confirm `/setup` is blocked once an operator exists.

---

## Phase C — Data-Root Override + `ConfigurationLoader` Fix

### Mechanism: one environment variable, `VIDEOFORENSICS_DATA_ROOT`

Read directly by `StorageLocationProvider.GetDefaultRoot()` — no pre-DI JSON config layer needed, since env vars are already visible to every process (console, WebApp, MAUI, and the Windows Service) before any DI container exists.

**Confirmed via direct read that only one file needs this change**: `src/core/helpers/core.helpers/Platform/StorageLocationProvider.cs`. `GetDefaultRoot(StorageCategory category)` currently hardcodes `Environment.GetFolderPath(CommonApplicationData)\VideoForensics` (Windows) / `/var/lib/videoforensics` (Linux) as `root` before the per-category `switch`. Change: at the top of the method, check `Environment.GetEnvironmentVariable("VIDEOFORENSICS_DATA_ROOT")`; if non-empty, use it as `root` in place of the hardcoded computation, on both the Windows and Linux branches (same variable name works cross-platform since it's just a root path). `GetEffectiveRoot()` needs no change — it already layers the DB-persisted override on top of whatever `GetDefaultRoot()` returns.

**Also confirmed via direct read: no change needed in `AddVideoForensicsSqlite`** (`src/data/database/sqlite/data.database.sqlite/DependencyInjection/ServiceCollectionExtensions.cs`) — it already calls `StorageLocationProvider.GetDefaultRoot(StorageCategory.Database)` when `dbPath` is null, so the env var fix above automatically flows through to the SQLite path with zero additional wiring. No `Program.cs`/`MauiProgram.cs` changes needed either, for the same reason — the env var is read lazily at DI-registration time, which is already after process/service startup has populated it.

For the Windows Service specifically: services don't reliably inherit user-session `setx`-set env vars. The installer (Phase D) must write the variable into the service's own registry `Environment` value (`HKLM\SYSTEM\CurrentControlSet\Services\VideoForensics\Environment`, REG_MULTI_SZ) rather than relying on machine-wide `setx`, so the service process is guaranteed to see it after a restart.

### `ConfigurationLoader` bug fix (adjacent, unrelated to the env var mechanism)

`src/client/core/VideoForensics.Client.Core/ConfigurationLoader.cs`, `LoadAndApplyAsync` — confirmed via direct read that only `DownloadLocation`/`QueryExportLocation` are copied from `loadedConfig` to `runtimeConfig` (lines 31-32); `DatabaseLocation`, `TempDownloadLocation`, `LogsLocation`, `ReportsLocation` are silently dropped. Add the 4 missing assignments alongside the existing ones. Note: this fix only affects propagation into the in-memory config object (used for display/consistency in the Storage Settings UI) — it does not and cannot fix "DB path at startup," which is exactly why the env var mechanism above (not this fix) is what makes the installer's data-path field meaningful.

### Verification

- Unit test on `LoadAndApplyAsync`: seed distinct values for all 6 location properties, confirm all 6 propagate (should fail against current code, pass after fix).
- Unit test on `GetDefaultRoot`: unset `VIDEOFORENSICS_DATA_ROOT` → unchanged existing paths (regression guard); set it to a temp path → all 7 `StorageCategory` values resolve under that root.
- Manual: set the registry `Environment` value for the `VideoForensics` service, restart it, confirm the DB file is created under the new root and `%ProgramData%\VideoForensics` is untouched.

---

## Phase D — Integration

- Add one genuine custom Inno wizard page here (the only place custom Pascal Script UI is justified — `[Components]`/`[Tasks]` can't collect free text): `CreateInputDirPage` for "Data Directory," defaulting to blank/greyed "use default (%ProgramData%\VideoForensics)".
- In `[Code]`'s `CurStepChanged(ssPostInstall)`, if a non-default data directory was entered, write it into the service's registry `Environment` value via `RegWriteMultiStringValue` (per Phase C's note) before starting the service.
- Final doc pass: confirm `deploy/windows/README.md` describes the data-directory picker and that `README.md`/`deploy/README.md` stay consistent now that both installer pieces are real.

### Verification

Fresh install with a custom data directory selected, confirm the service starts pointed at the custom root, confirm the existing Storage Settings UI (`/settings/storage`) reflects the correct effective path.

---

## CI validation order

Trigger `publish-dev.yml` (push to `wip`) first to validate the new `ISCC.exe` CI step end-to-end at the lowest-risk tier, before relying on the same pattern in `release-installers.yml`'s tagged releases.
