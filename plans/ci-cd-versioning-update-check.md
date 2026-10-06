# GitHub CI/CD Pipeline, Nerdbank.GitVersioning, and Update-Check Feature

## Context

VideoForensics currently has two disconnected, gap-ridden pieces of release infrastructure:

- **`.github/workflows/cibuild.yml`** is a stale "does it build" check — pinned to a .NET 8 action while the codebase is actually .NET 10, runs no tests, and never publishes anything.
- **`.github/workflows/release-installers.yml`** is a newer (still "shakedown build" / unvalidated on real Windows) 296-line workflow that *does* build a Windows MSI+bootstrapper and a Debian `.deb` on `v*` tag pushes, but only ever uploads them to a **draft** GitHub Release — nothing is published to a package registry, and there's no way for an already-installed server to discover that a new version exists.
- **Versioning is split across two disconnected schemes**: `src/Directory.Build.props` has a custom MSBuild target deriving `1.0.0.<git commit count>` (never advances past `1.0.0`, not tag-aware, not real semver), while `release-installers.yml` separately strips `v` off the pushed git tag for the installer version. `VideoForensics.WebApp.csproj` also hardcodes `<Version>1.0.0</Version>`, which `INSTALLER_PLAN.md` already flags as the intended single source of truth but currently isn't. These need to collapse into one consistent, tag-driven scheme — the user has asked for **Nerdbank.GitVersioning (NBGV)** specifically.
- **No "check for updates" capability exists anywhere** — confirmed absent by search, and explicitly deferred as out-of-scope in `INSTALLER_PLAN.md`. The user wants the installed server to periodically check GitHub for a newer release and, per a configurable setting, either just notify or actually download+launch the update.

The intended outcome: a real CI pipeline (build+test on every PR/push, publish gated to tags), a single trustworthy version number flowing from git tags through the assembly, the installer, and into what the update-checker compares, and a first-cut `UpdateCheckService` following this repo's existing `DeviceHealthSyncService`/`CloudflaredTunnelService` patterns.

### Decisions already confirmed (do not re-ask)
- **Two channels**: `main` is the **Stable** channel (existing tag-gated `v*` flow, draft GitHub Release, unchanged semantics), `wip` is the **Dev** channel (every push rebuilds and republishes a single rolling `dev` prerelease GitHub Release — tag `dev`, `prerelease: true`, overwritten each push, containing the latest unsigned installers built from `wip`). Both branches get full CI (build+test); only `main` tags and `wip` pushes trigger a publish.
- Publish to **both** GitHub Packages (NuGet) and GitHub Releases (installers).
- CI runs build+test on every PR and every push to `main` **and** `wip`; publishing is gated — `main` publishes only on `v*` tag pushes (stable), `wip` publishes on every push (rolling dev prerelease).
- Update-check ships as a **configurable** setting: `NotifyOnly` or `AutoDownloadAndInstall`, not hardcoded to one behavior.
- Use **Nerdbank.GitVersioning**, replacing the custom `SetApiVersionFromGit` target.
- NuGet packaging scope: **Contracts/common libraries only** (`VideoForensics.Providers.Common`, `VideoForensics.Api.Contracts`, `VideoForensics.Data.Common`, `VideoForensics.Core.Logging`) — not a solution-wide pack.
- **No container image / Dockerfile** for `VideoForensics.WebApp` in this effort (native ffmpeg/cloudflared bundling + DPAPI key storage make this nontrivial; skip, revisit separately if wanted).
- The GitHub Release created on tag push **stays a draft** (unsigned installers still need manual review before going live) — no change to `create-release`'s `draft: true`.
- Repo is `rsperry79/VideoForensics`, default branch is `main`.
- **Promotion flow**: a Stable release is cut by opening a PR from `wip` into `main`, merging (required now that `main` is PR-only), then pushing the `vX.Y.Z` tag on `main`. `wip` itself is never tagged directly — this keeps `main`'s history exactly matching what's tagged.
- **Release notes**: `publish.yml`/`publish-dev.yml` pull the relevant section out of `CHANGELOG.md` into the GitHub Release body, rather than hand-written boilerplate.
- **Update-check UI**: a minimal Blazor panel ships in this same pass (not deferred) — current version, "update available" state, and a check-now button.
- Config persistence for the new update-check settings needs **no schema/migration work** — `IForensicsConfigurationService`/`ForensicsConfigurationService` (`src/client/core/VideoForensics.Client.Core/Services/ForensicsConfigurationService.cs`) is a flat key/value store via `IAppSettingRepository`; new bool/enum/int properties just need `Get*Setting`/`SetAsync` lines added alongside the existing ones (e.g. `EnableHealthSync`).

---

## Part 1 — Nerdbank.GitVersioning

1. **New `version.json`** at repo root (NBGV's "cloud build root" — correct here given the single `VideoForensics.sln`):
   ```json
   {
     "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
     "version": "1.0-alpha",
     "publicReleaseRefSpec": [ "^refs/tags/v\\d+\\.\\d+\\.\\d+$" ],
     "cloudBuild": { "buildNumber": { "enabled": true } },
     "nugetPackageVersion": { "semVer": 2 },
     "assemblyVersion": { "precision": "build" }
   }
   ```
   The anchored `publicReleaseRefSpec` means only a real `v1.2.3`-shaped tag produces a clean release version; every other build (PRs, `main` pushes, local dev) gets NBGV's default `-gCOMMITHASH`-style prerelease suffix — this is what both the CI pipeline and the update-check comparison rely on.

2. **`src/Directory.Build.props`**: delete the `SetApiVersionFromGit` target and its explanatory comment, delete the static `<Version>`/`<AssemblyVersion>`/`<FileVersion>` fallback properties (NBGV's own MSBuild targets set these; leaving static values here would shadow NBGV). Add `<PackageReference Include="Nerdbank.GitVersioning" Version="3.7.x" PrivateAssets="all" />` (check latest 3.x on NuGet at implementation time) to the shared `ItemGroup` so every project gets it. Leave `NoWarn`, `TestingPlatformDotnetTestSupport`, and the `Microsoft.Testing.Platform` reference untouched.

3. **`src/client/web/VideoForensics.WebApp/VideoForensics.WebApp.csproj`**: remove the hardcoded `<Version>1.0.0</Version>` line — this is the exact conflict `INSTALLER_PLAN.md` already flagged. Grep the rest of `src/**/*.csproj` for any other static `Version`/`AssemblyVersion`/`FileVersion` overrides and remove those too (none found in exploration, but verify).

4. **`.github/workflows/release-installers.yml`**: add `fetch-depth: 0` to the `actions/checkout@v4` steps in `build-debian` and `build-windows` (NBGV needs full tag history). Replace the manual "strip `v` from `GITHUB_REF_NAME`" bash/pwsh steps with a read of NBGV's computed version, e.g.:
   ```bash
   VERSION=$(dotnet msbuild src/client/web/VideoForensics.WebApp/VideoForensics.WebApp.csproj -getProperty:Version)
   VERSION="${VERSION%%+*}"   # strip +buildmetadata — WiX ProductVersion can't parse it
   echo "version=$VERSION" >> "$GITHUB_OUTPUT"
   ```
   (pwsh equivalent for the Windows job.) Everything downstream that consumes `steps.*.outputs.version` (WiX `DefineConstants`, `.deb` build script, sha256 filenames) is unchanged — only the source of that value changes. Keep `create-release`'s `draft: true` and its `if: startsWith(github.ref, 'refs/tags/v')` gate as-is.

5. **Update-check version comparison source**: with NBGV in place, read the running app's version via `FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion` (carries the full computed semver incl. prerelease/git-height suffix) rather than `AssemblyVersion` (numeric-only, loses prerelease info). A running dev/prerelease build must never be treated as "newer" than a tagged release just because of its git-height suffix — see Part 3.

**Verification**: `dotnet build VideoForensics.sln` succeeds locally and a built DLL's file version reflects an NBGV-derived value (not `1.0.0.0`). This is the lite gate for Part 1 before moving on.

---

## Part 2 — GitHub Actions CI/CD

1. **`.github/workflows/ci.yml`** (new, replaces `cibuild.yml`): triggers on `push`/`pull_request` to **both `main` and `wip`**. Single job, `ubuntu-latest`, `fetch-depth: 0` (needed even for PR builds since every build now computes an NBGV version), `dotnet-version: 10.0.x` with `cache: true` on `actions/setup-dotnet@v4` (built-in NuGet restore caching — free, cuts CI time on every run), then plain `dotnet restore VideoForensics.sln` / `dotnet build ... --configuration Release --no-restore` / `dotnet test ... --no-build --logger trx --results-directory TestResults`, with results uploaded via `actions/upload-artifact@v4` (`if: always()`). Add a `dependency-review` job (using `actions/dependency-review-action`, PR-triggered only) alongside the build job — free for this repo's tier, flags PRs that introduce a known-vulnerable package dependency, complementing Dependabot (catches *outdated* deps) and CodeQL (catches *code* issues) with the one gap neither covers. Keep this to exactly the commands a developer already runs locally — no logic buried only in YAML — since that's what satisfies "locally testable" here (recommend against depending on `act`; it has known friction with this repo's multi-project, self-contained-publish, Windows-installer-adjacent workflows).
2. **Delete `.github/workflows/cibuild.yml`** in the same change — don't leave two build workflows firing on every push.
3. **`.github/workflows/publish.yml`** (new) — **Stable channel**, triggered on `push: tags: v*` + `workflow_dispatch`, kept **separate** from `release-installers.yml` (lower regression risk than merging into the not-yet-Windows-validated installer workflow). One job, `publish-nuget`:
   - `actions/checkout@v4` with `fetch-depth: 0`, setup .NET 10.
   - `dotnet pack` the four confirmed projects (`VideoForensics.Providers.Common`, `VideoForensics.Api.Contracts`, `VideoForensics.Data.Common`, `VideoForensics.Core.Logging`) at `-c Release`.
   - `dotnet nuget push "**/*.nupkg" --api-key ${{ secrets.GITHUB_TOKEN }} --source https://nuget.pkg.github.com/rsperry79/index.json --skip-duplicate`, gated with `if: startsWith(github.ref, 'refs/tags/v')` so `workflow_dispatch` can dry-run the pack step without pushing.
   - Needs a `permissions: packages: write` block.
   - The `create-release` job in `release-installers.yml` (unchanged file, still builds the installers) gets one small addition: a step that extracts the section of `CHANGELOG.md` matching the tag's version (a simple `awk`/`sed` block between the `## [X.Y.Z]` heading and the next `## [` heading, falling back to the existing hand-written boilerplate if no matching section is found) and passes it via `body_path` to `softprops/action-gh-release@v2` instead of the current inline `body:` block — the "unsigned installers" disclaimer text stays, appended after the extracted notes.
4. **`.github/workflows/publish-dev.yml`** (new) — **Dev channel**, triggered on `push: branches: [wip]`, with a workflow-level `concurrency: { group: publish-dev, cancel-in-progress: true }` — without this, two rapid pushes to `wip` could run in parallel and race to update the same rolling `dev` tag/release/assets; the concurrency group ensures a newer push cancels an in-flight older run rather than both fighting over the same release. Mirrors `release-installers.yml`'s `build-debian`/`build-windows` structure (same publish/bundle/WiX/`.deb`-build steps, `fetch-depth: 0` for NBGV) but instead of extracting a version from a tag, uses the NBGV-computed prerelease version directly (`dotnet msbuild ... -getProperty:Version`, `+buildmetadata` stripped same as Part 1.4 — a `wip` push is never a `publicReleaseRefSpec` match, so NBGV naturally emits a prerelease-suffixed version like `1.0.1-alpha.g<hash>`, which both WiX and the `.deb` control file can accept as a version string once any `+` metadata is stripped). A third job, `publish-dev-release`, downloads both installer artifacts and calls `softprops/action-gh-release@v2` with a **fixed tag `dev`**, `prerelease: true`, no `draft`, and a `body_path` pointing at the `## [Unreleased]` section of `CHANGELOG.md` (same extraction approach as the Stable-channel step above, just targeting the `Unreleased` heading instead of a version heading) — `action-gh-release` overwrites the existing release+assets on the same tag, so this becomes a single rolling "latest dev build" release rather than accumulating one release per push. Also `dotnet pack`+`dotnet nuget push` the same four library projects as `publish-nuget`, so dev-channel prerelease packages land in GitHub Packages too (NuGet allows prerelease versions natively — no `--skip-duplicate` collision risk since each `wip` push produces a distinct git-height-suffixed version). Needs `permissions: contents: write, packages: write`.
5. **New `nuget.config`** at repo root with a `github` package source: `https://nuget.pkg.github.com/rsperry79/index.json` (none exists today).
6. **`ci.yml` permissions**: `contents: read` only (no publish surface there).

**Verification**: push a PR and confirm `ci.yml` runs build+test on both `main`- and `wip`-targeted PRs; a `workflow_dispatch` run of `publish.yml` (without a real tag) confirms the pack step produces valid `.nupkg`s without attempting a push; a push to `wip` confirms the rolling `dev` prerelease release updates in place (same URL, refreshed assets) rather than creating a new release each time.

---

## Part 3 — Update-check feature

1. **`src/client/host/VideoForensics.Hosting/GitHubReleaseClient.cs`** (new, single file, interface co-located like `CloudflaredTunnelService.cs`'s convention):
   ```csharp
   public record GitHubReleaseInfo(string TagName, string HtmlUrl, bool Draft, bool Prerelease, IReadOnlyList<GitHubReleaseAsset> Assets);
   public record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size);
   public interface IGitHubReleaseClient
   {
       Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct);
       Task<GitHubReleaseInfo?> GetLatestDevReleaseAsync(CancellationToken ct);
   }
   ```
   `GetLatestReleaseAsync` calls `GET /repos/rsperry79/VideoForensics/releases/latest` (GitHub's `/latest` endpoint already excludes drafts and prereleases, matching the stable channel). `GetLatestDevReleaseAsync` calls `GET /repos/rsperry79/VideoForensics/releases/tags/dev` (the rolling dev-channel release from Part 2.4's `publish-dev-release` job). Both go through an `IHttpClientFactory`-registered typed client (new pattern for this repo — no existing `AddHttpClient` usage found). Must set a `User-Agent` header (GitHub rejects unauthenticated requests without one). Both return `null` on any non-success status or malformed JSON rather than throwing — callers treat `null` as "check failed, retry next tick." No auth token: this runs on the *installed server* at runtime, not in CI, so there is no `GITHUB_TOKEN` available and embedding a PAT would violate "no secrets in code" — unauthenticated's 60 req/hr limit is ample at a 24h+ polling interval.

2. **TDD**: `src/client/host/VideoForensics.Hosting.Tests/GitHubReleaseClientTests.cs` first — mock `HttpMessageHandler`/typed client, cover: successful parse of a realistic release payload, 404 → `null`, 403 (rate-limited) → `null`, malformed JSON → `null`, none throw.

3. **`src/client/common/VideoForensics.Client.Common/Contracts/IForensicsConfiguration.cs`**: add to the interface + `ForensicsConfiguration` implementation, matching the existing `EnableHealthSync` bool-toggle convention:
   - `bool EnableUpdateCheck { get; set; }` (default `true`)
   - `UpdateCheckMode UpdateMode { get; set; }` (default `NotifyOnly`)
   - `int UpdateCheckIntervalHours { get; set; }` (default `24`)
   - `UpdateReleaseChannel ReleaseChannel { get; set; }` (default `Stable`) — selects which of `IGitHubReleaseClient`'s two methods `UpdateCheckService` calls each tick, letting an operator opt a server into tracking the rolling `dev` prerelease instead of tagged Stable releases.
   - New enums `UpdateCheckMode { NotifyOnly, AutoDownloadAndInstall }` and `UpdateReleaseChannel { Stable, Dev }`.
   Then wire the four new properties into `ForensicsConfigurationService.LoadFromDatabaseAsync`/`SaveToDatabaseAsync` (`src/client/core/VideoForensics.Client.Core/Services/ForensicsConfigurationService.cs`) alongside the existing `EnableHealthSync` lines — same `GetBoolSetting`/`GetEnumSetting`/`GetIntSetting` helpers, no new persistence plumbing needed.

4. **`src/client/host/VideoForensics.Hosting/BackgroundServices/UpdateCheckService.cs`** (new): interface + state record + `BackgroundService` implementation, directly mirroring `DeviceHealthSyncService.cs`'s shape:
   ```csharp
   public record UpdateCheckState(bool UpdateAvailable, string? LatestVersion, string CurrentVersion, string? DownloadUrl, DateTime? LastCheckedUtc, string? ErrorMessage);
   public interface IUpdateCheckService { UpdateCheckState GetState(); Task TriggerCheckNowAsync(CancellationToken ct); }
   ```
   - `PeriodicTimer` sized from `_config.UpdateCheckIntervalHours` (read once at construction, same as `DeviceHealthSyncService`'s fixed-interval pattern — a config change takes a restart), `do/while` loop gating on `_config.EnableUpdateCheck` with `continue` (not `return`) each tick so a toggle takes effect without restart.
   - `internal async Task RunOneTickAsync(CancellationToken ct)`: read current version (Part 1.5's `FileVersionInfo` approach, try/catch → `ErrorMessage` on failure) → call `IGitHubReleaseClient.GetLatestReleaseAsync` or `GetLatestDevReleaseAsync` depending on `_config.ReleaseChannel` (null/exception → `ErrorMessage` set, `LastCheckedUtc` still updated, no throw) → parse `TagName` stripped of leading `v` as `major.minor.patch` (unparseable → `ErrorMessage`, no throw) → compare against current, treating any prerelease/git-height-suffixed running version conservatively (never "newer" than a clean tag) → pick the matching platform asset (`.exe` bootstrapper on Windows via `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`, `.deb` on Linux; no match → still report `UpdateAvailable = true` with `DownloadUrl = null`) → if available and `UpdateMode == AutoDownloadAndInstall`, invoke the download/launch step (own try/catch, isolated from the rest of the tick) → always update the `lock`-protected state snapshot at the end (`GetState()` pattern copied from `CloudflaredTunnelService`).
   - Introduce a small `IUpdateDownloader`/installer-invocation seam (interface) so the download+launch step is mockable in tests without hitting the filesystem or spawning real processes.
   - **Windows path**: download the bootstrapper `.exe` asset to a temp path, launch via `CliWrap` fire-and-forget (`Cli.Wrap(path).WithArguments("/passive").ExecuteAsync(ct)` without awaiting inside the tick). Confirmed by reading `deploy/windows/VideoForensics.Bootstrapper.Wix/Bundle.wxs`: it uses `BootstrapperApplicationRef Id="WixStandardBootstrapperApplication.HyperlinkLicense"`, WiX's standard Burn bootstrapper, which natively supports `/quiet`/`/passive`/`/norestart` — no WXS changes needed, just pass `/passive` (shows a progress UI but no click-through wizard) rather than launching with no arguments (which would pop the full interactive wizard, defeating "auto-install"). Confirmed by reading `deploy/windows/VideoForensics.Installer.Wix/Package.wxs`: it already declares `ServiceInstall`/`ServiceControl` elements, so the MSI itself stops/reinstalls/restarts the Windows Service as part of the upgrade — `UpdateCheckService` doesn't need to orchestrate its own shutdown. The bootstrapper still triggers its own UAC elevation prompt (expected; it's unsigned, no cert yet) — that one prompt is unavoidable without a code-signing certificate and is out of scope here.
   - **Debian path**: download the `.deb` to a well-known pending-update location; **do not** attempt unattended `dpkg -i` (needs root; silently self-modifying a running systemd-managed service's binaries unattended is out of scope per the goal's own framing). Confirmed by reading `deploy/debian/postinst`: it already runs `systemctl` restart/reload logic on package upgrade, so once an admin runs `sudo dpkg -i <path>` manually, the service restart happens automatically with no additional plan work needed — the only gap for v1 is that the *download* step doesn't yet trigger dpkg itself. Surface "downloaded, run `sudo dpkg -i <path>` to finish" in `UpdateCheckState`/logs. Document this Windows/Debian asymmetry in the class's XML doc comment, same style as `CloudflaredTunnelService`'s scope-limit comments.

5. **TDD**: `src/client/host/VideoForensics.Hosting.Tests/UpdateCheckServiceTests.cs` first, following `DeviceHealthSyncServiceTests.cs`'s `CreateService(...)` + `Mock<T>` + direct `RunOneTickAsync` invocation pattern. Cases: disabled-via-config no-op; newer version available sets `UpdateAvailable=true`; already-latest sets `UpdateAvailable=false`; `NotifyOnly` mode never invokes the downloader; `AutoDownloadAndInstall` mode invokes it with the expected asset URL; GitHub API returns null → graceful, no throw; GitHub API throws → swallowed, no throw (matches `DeviceHealthSyncService`'s existing throw-swallowing test); malformed release tag → graceful; no matching platform asset → `UpdateAvailable=true`, `DownloadUrl=null`; `TriggerCheckNowAsync` invokes the same tick logic.

6. **`src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs`**: inside `AddVideoForensicsServerCore` (alongside the existing `AddHostedService<DeviceHealthSyncService>()` call), register:
   ```csharp
   services.AddHttpClient<IGitHubReleaseClient, GitHubReleaseClient>(client =>
   {
       client.BaseAddress = new Uri("https://api.github.com/");
       client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoForensics-UpdateCheck/1.0");
   });
   services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
   services.AddHostedService(sp => (UpdateCheckService)sp.GetRequiredService<IUpdateCheckService>());
   ```
   This is server-tier only — never referenced from `VideoForensics.MauiApp`/`MauiProgram.cs`, per CLAUDE.md's client/server split (`UpdateCheckService` talks to a provider-equivalent external API directly, same category of thing `DeviceHealthSyncService` does).

7. **`src/client/web/VideoForensics.WebApp/Api/UpdateCheckEndpoints.cs`** (new), following the **majority, CLAUDE.md-mandated** endpoint convention (`/api/v1/...` — confirmed by grepping `Api/*.cs`: most endpoint files use `/api/v1/...`; `NetworkSettingsEndpoints`/`DeviceManagementEndpoints`/`RemoteAccessEndpoints`/`NotificationEndpoints` are unversioned pre-existing outliers, not the pattern to copy): `GET /api/v1/update-check` returns `IUpdateCheckService.GetState()`, `POST /api/v1/update-check/check-now` calls `TriggerCheckNowAsync` then returns the refreshed state, both under `RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)`. Register with `app.MapUpdateCheckEndpoints();` in `Program.cs` alongside the other `Map...Endpoints()` calls.
8. **`src/core/api/VideoForensics.Api.Contracts/UpdateCheckStateDto.cs`** (new): a DTO mirroring `UpdateCheckState`'s fields, plus `ToDto()`/`ToDomain()` mapping extension methods (CLAUDE.md's mandated naming — never `FromDto`) — needed because the client/server split rules require wire calls to use DTOs from `Api.Contracts`, never a bare domain record, once a `Remote*` client exists (next step).
9. **`src/client/host/VideoForensics.Hosting/Remote/RemoteUpdateCheckService.cs`** (new): implements `IUpdateCheckService` by calling `GET`/`POST /api/v1/update-check...` via the same paired-device bearer-token auth every other `Remote*` class in that folder already uses (no new auth mechanism) — follow `RemoteForensicsConfigurationService.cs`'s exact shape. Registered only in `AddVideoForensicsClientApi` (MAUI-side DI), never in `AddVideoForensicsServerCore` — same interface name (`IUpdateCheckService`) as the server-side registration, so no `.razor` file needs to know which one it's getting.
10. **Blazor UI panel** — new `src/client/ui/VideoForensics.Ui.Shared/Pages/UpdateCheck.razor` (or a section added to an existing Settings-style page, matching whatever this repo's actual Settings navigation convention is — check `NavGroups.cs`/existing `Pages/*.razor` for where an admin-facing operational toggle like this belongs, e.g. alongside `NetworkSettings.razor`/`RemoteAccess`-equivalent pages): `@inject IUpdateCheckService` (silently resolves to the local server implementation when running in `VideoForensics.WebApp`, or `RemoteUpdateCheckService` when running in MAUI, per the client/server split's DI-substitution guarantee), displaying current version, an "update available" banner with the latest version/release notes link when applicable, and a "Check now" button wired to `TriggerCheckNowAsync`. Read-only for actual update data (matches this project's existing read-only-UI convention for forensic data) — the one write action is the operator-triggered check-now poll, consistent with existing operational controls elsewhere in the UI (e.g. the Remote Access panel's tunnel start/stop), not a data-editing operation.

**Verification (lite gate)**: `dotnet build`/`dotnet test` on `VideoForensics.Hosting`, `VideoForensics.Hosting.Tests`, and `VideoForensics.WebApp` after each batch of changes. Manually hit `GET /api/update-check` against a running instance to confirm it returns a sane `UpdateCheckState` (even before any real tagged release exists, it should report `UpdateAvailable=false` or a handled error, never throw/500).

---

## Part 4 — Branch protection and free repo hygiene checks

1. **`main` branch protection (repo setting, not a file change)**: require every change to land via PR (no direct pushes to `main`), require the `ci.yml` build+test job to pass as a required status check, require the branch to be up to date before merging, and disallow force-pushes/branch deletion on `main`. This is a GitHub repository setting (`PATCH /repos/rsperry79/VideoForensics/branches/main/protection` via `gh api`, or the Settings → Branches UI) — **not** a file in the repo, and it's the kind of "modify account/repo settings" action that needs the user's explicit go-ahead at execution time rather than being silently applied by a Haiku subagent mid-plan. Surface the exact `gh api` command for the user to run (or run it only after they confirm) rather than doing it unprompted. `wip` is left unprotected (direct pushes allowed), matching its role as the fast-moving dev channel.
2. **`.github/dependabot.yml`** (new): weekly update checks for the `nuget` ecosystem (root `directory: "/"`, since `VideoForensics.sln` covers the whole tree) and the `github-actions` ecosystem (keeps `actions/checkout@v4`, `actions/setup-dotnet@v4`, `softprops/action-gh-release@v2`, etc. current) — free on all repos, just a config file, opens PRs automatically that then run through `ci.yml` like any other PR.
3. **`.github/workflows/codeql.yml`** (new): GitHub CodeQL static analysis for C#, using `github/codeql-action/init` + `autobuild` + `analyze`, triggered on `push`/`pull_request` to `main` and `wip` plus a weekly `schedule` cron — free for this repo's visibility tier, catches real security/correctness issues (injection, unsafe deserialization, etc.) beyond what `dotnet build`/tests cover.
5. **`dependency-review` job** (in `ci.yml`, PR-triggered only, per Part 2.1 above): `actions/dependency-review-action` fails a PR that introduces a known-vulnerable dependency — free, and the one gap neither Dependabot (outdated, not necessarily vulnerable) nor CodeQL (code, not dependency metadata) covers.
4. **Secret scanning / push protection**: GitHub's secret scanning is typically already on by default for this repo's tier — no file or workflow needed, just worth the user double-checking it's enabled under Settings → Code security, called out here so it isn't assumed silently covered.

**Verification**: after Dependabot/CodeQL files land, confirm via the repo's Security tab (or `gh api repos/rsperry79/VideoForensics/code-scanning/alerts`) that CodeQL actually ran and Dependabot opened its first scan; branch protection is verified by attempting a direct push to `main` and confirming it's rejected (or by reading back `gh api repos/rsperry79/VideoForensics/branches/main/protection`).

---

## Part 5 — Rewrite root-level docs to reflect what this repo actually is

`README.md` at repo root is leftover from the original `Ring.Api` library this project forked from — it describes a NuGet package called "Ring.Api" for talking to Ring doorbells, with a `Ring/Ring.Api` GitHub org, a `master`-branch CI badge pointing at `cibuild.yml`, and a version history table ending at "1.0.0.0 released Oct 23, 2024" for that unrelated library. None of it describes VideoForensics (a multi-provider — Ring/Wyze/Uniview — video forensics platform with a WebApp, MAUI client, MCP server, installers, chain-of-custody/evidence features). There is no `CHANGELOG.md` at all today.

1. **`README.md`** (rewrite): replace entirely with a description of what VideoForensics actually is — a forensic video evidence capture/management platform across Ring/Wyze/Uniview providers, with a WebApp + MAUI desktop client + MCP server, self-contained Windows MSI and Debian `.deb` installers, chain-of-custody/evidence-report features. Include: a short project description, links to `deploy/README.md` (install instructions) and `INSTALLER_PLAN.md` if still present, a CI badge pointing at the new `ci.yml` (not the dead `cibuild.yml`/`Ring/Ring.Api` badge), a note on the two channels (Stable releases off `main`/tags, rolling Dev prerelease off `wip`) and where to get each (GitHub Releases page, the `dev` prerelease). Drop the old Ring.Api NuGet version-history table entirely — replaced by the new `CHANGELOG.md`.
2. **`CHANGELOG.md`** (new, repo root): seed using [Keep a Changelog](https://keepachangelog.com/) format with an `## [Unreleased]` section at the top. Going forward this is hand-maintained per CLAUDE.md's existing "Breaking changes: update this file and commit message" documentation rule — this plan only seeds the file and a first entry describing the CI/NBGV/update-check work itself once it lands; it does not attempt to backfill history for prior unreleased commits.
3. **`LICENSE`** (new, repo root): all-rights-reserved copyright notice (no open-source grant) — chosen because Syncfusion (see below) already gates the build behind a separate commercial license, so an open license on this repo's own code wouldn't actually enable free build/use, and this is a single-org forensics tool with provider-integration code where redistribution control matters.
4. **`CREDITS.md`** (new, repo root): lists third-party components this repo depends on or bundles, with their own license terms called out explicitly (this repo's LICENSE does not extend to any of these): **Syncfusion Essential Studio** (commercial — confirmed via `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(...)` in both `VideoForensics.WebApp/Program.cs` and `VideoForensics.MauiApp/MauiProgram.cs`, reading a key from a file path at runtime, not embedded; anyone building this repo needs their own valid Syncfusion license, paid or Community), **ffmpeg** (LGPL, bundled binary in installers per `release-installers.yml`), **cloudflared** (Apache 2.0, bundled binary), **WiX Toolset** (MS-RL, used to build the Windows installer). Also credit the original `Ring.Api` project this repo forked from (MIT), since the old README traced back to it.
5. Leave `CLAUDE.md` and `JAMMING_ANALYSIS_GUIDE.md` as-is — `CLAUDE.md` is Claude-specific project instructions (already accurate) and `JAMMING_ANALYSIS_GUIDE.md` is a feature-specific guide, not a project-identity doc; neither is what "reflect what this actually is" is pointing at.

**Verification**: read the rewritten `README.md` back and confirm it no longer mentions Ring.Api/the `Ring/Ring.Api` org/`master` branch anywhere, and that its CI badge URL matches the real `ci.yml` workflow file name.

---

## Part 6 — Thin install-script bootstrap (avoids install-then-immediately-update)

Today's GUI installer (MSI/bootstrapper `.exe`, `.deb`) is built once per CI run and bakes in whatever version was current at that moment — if a newer release lands before a user clicks install, they get stale bits and `UpdateCheckService` immediately nags them to re-download. Fix: add thin bootstrap scripts that fetch the actual current release at *run time* instead of at CI *build* time — the same "fetch latest, no version pinning" pattern this repo already uses for cloudflared/ffmpeg in `release-installers.yml` (those are pulled from a rolling "latest" URL at CI build time, not pinned to a specific version).

1. **`deploy/install.ps1`** (new, Windows): a small PowerShell script that calls the GitHub Releases API (`https://api.github.com/repos/rsperry79/VideoForensics/releases/latest` for Stable, or `.../releases/tags/dev` for Dev — accept a `-Channel Stable|Dev` parameter, default `Stable`), finds the `VideoForensicsBootstrapper.exe` asset for the chosen channel/release, downloads it to a temp path, and launches it (interactive by default; support a `-Silent` switch that passes `/passive` to the downloaded bootstrapper, matching `UpdateCheckService`'s own invocation in Part 3.4). No hash pinning (same trust model as the existing cloudflared/ffmpeg "latest" download steps already in this repo) — relies on HTTPS transport security, not a new risk category for this repo.
2. **`deploy/install.sh`** (new, Debian): equivalent bash script — calls the GitHub Releases API for the chosen channel, downloads the matching `.deb` asset, and either runs `sudo dpkg -i` directly (if invoked with root/sudo) or prints the manual command, matching the existing manual-install expectation from Part 3.5's Debian auto-update path. Accept a `--channel stable|dev` flag, default `stable`.
3. The existing GUI installer (MSI/bootstrapper `.exe`) and `.deb` artifacts **still get built and published exactly as today** (Parts 1/2 unchanged) — `install.ps1`/`install.sh` become the **recommended entry point** documented in the rewritten `README.md` (Part 5) instead of a direct "download this installer" link, but the direct download still works for anyone who wants it (e.g., air-gapped install, or just double-clicking from the Releases page).
4. `README.md`'s install instructions (Part 5.1) should lead with a one-line `irm https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.ps1 | iex`-style Windows command and a `curl -fsSL https://raw.githubusercontent.com/rsperry79/VideoForensics/main/deploy/install.sh | sudo bash` Debian command, with the direct-download Releases page link kept as a secondary option.

**Verification**: run `deploy/install.ps1 -Channel Dev` (or `.sh --channel dev`) against the live repo once a `dev` prerelease exists and confirm it downloads and launches/installs the correct current asset, not a stale one.

---

## Execution order (for Haiku subagent dispatch, per file)

1. `version.json`
2. `src/Directory.Build.props`
3. `VideoForensics.WebApp.csproj` version cleanup + grep sweep for other static versions
4. Lite gate: `dotnet build VideoForensics.sln`
5. `.github/workflows/ci.yml` (new) + delete `cibuild.yml`
6. `.github/workflows/release-installers.yml` version-source edit
7. `nuget.config` (new)
8. `.github/workflows/publish.yml` (new, Stable channel)
9. `.github/workflows/publish-dev.yml` (new, Dev channel — rolling `dev` prerelease)
10. `IForensicsConfiguration.cs` + `ForensicsConfiguration` defaults + `UpdateCheckMode`/`UpdateReleaseChannel` enums
11. `ForensicsConfigurationService.cs` load/save wiring for the 4 new settings
12. `GitHubReleaseClient.cs` — write `GitHubReleaseClientTests.cs` first (TDD), confirm failing, then implement
13. `UpdateCheckService.cs` — write `UpdateCheckServiceTests.cs` first (TDD), confirm failing, then implement
14. `VideoForensicsHostingExtensions.cs` DI registration (server-side `IUpdateCheckService`)
15. `UpdateCheckStateDto.cs` + `ToDto()`/`ToDomain()` mapping in `VideoForensics.Api.Contracts`
16. `UpdateCheckEndpoints.cs` (`/api/v1/update-check`) + `Program.cs` registration
17. `RemoteUpdateCheckService.cs` (`Hosting/Remote/`) + its registration in `AddVideoForensicsClientApi` + a mocked-HTTP test alongside the existing `Remote*` test pattern
18. `Pages/UpdateCheck.razor` (or the equivalent Settings-page section) in `VideoForensics.Ui.Shared`
19. Lite gate: `dotnet build`/`dotnet test` on touched projects (`VideoForensics.Hosting`, `VideoForensics.Hosting.Tests`, `VideoForensics.WebApp`, `VideoForensics.Ui.Shared`)
20. `.github/dependabot.yml` (new)
21. `.github/workflows/codeql.yml` (new)
22. `main` branch protection — present the exact `gh api` command to the user and apply only after explicit confirmation (repo-setting change, not a file edit)
23. `README.md` rewrite
24. `CHANGELOG.md` (new)
25. `LICENSE` (new, all-rights-reserved)
26. `CREDITS.md` (new)
27. `deploy/install.ps1` (new)
28. `deploy/install.sh` (new)

The **full gate** (clean rebuild, solution-wide NuGet package updates, full test suite) happens later, before opening a PR, and requires explicit user confirmation first per CLAUDE.md — not part of this execution sequence.
