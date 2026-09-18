# Windows + Debian Installer with Update Support — Implementation Plan

## Goal

Ship `VideoForensics.WebApp` as a real installer on both target platforms, and make
upgrading an existing install a first-class, safe operation on each:

- **Windows:** a signed `.msi` (or `.exe` bootstrapper wrapping an MSI) that installs the
  app, registers/updates the Windows Service, and supports in-place upgrade to a newer
  version without a separate uninstall step.
- **Debian/Ubuntu (apt-based):** a `.deb` package that installs the app, registers/updates
  a `systemd` unit, and supports `apt upgrade`/`dpkg -i` over an existing install.

This replaces the current "publish + run a PowerShell script by hand" workflow
(`deploy/install-service.ps1`) with a packaged installer while keeping that script's
underlying mechanism (same binary, `UseWindowsService()` no-ops everywhere except under
the SCM) as the thing the installer drives, not something it reimplements.

## Confirmed starting point (read before planning)

- `VideoForensics.WebApp` (`src/client/web/VideoForensics.WebApp`, `net10.0`,
  `Microsoft.NET.Sdk.Web`) already calls `UseWindowsService()` unconditionally in
  `Program.cs` — safe no-op off Windows/outside the SCM.
- All persistent state (SQLite DB, DPAPI/keys, logs) is already rooted at
  `Environment.SpecialFolder.CommonApplicationData` + `VideoForensics`
  (`%ProgramData%\VideoForensics` on Windows; resolves to `/usr/share/VideoForensics`
  on Linux under a root-run process, or `$HOME/.local/share/VideoForensics` if ever run
  as a non-root user — the systemd unit must pin `User=`/`Group=` and, if root-owned data
  is undesired, an explicit `DOTNET_CommonApplicationData`-equivalent override so this is
  deterministic, not whoever's UID happens to launch the service).
- `deploy/install-service.ps1` / `deploy/uninstall-service.ps1` already implement:
  install-dir copy, Windows Event Log source creation, SCM service registration, failure
  recovery config, start/stop, and are already re-runnable for upgrades (stop, remove,
  reinstall). This is the logic an MSI's custom actions (or a WiX-driven install) should
  call into or mirror — not a green field.
- **No Linux/systemd support exists at all today** — no `.service` unit file, no
  `UseSystemd()` call in `Program.cs`, no `.deb` packaging, nothing under `deploy/` for
  Linux. This is net-new work, not a port of an existing script.
- `VideoForensics.WebApp.csproj` has no `RuntimeIdentifier`/self-contained publish
  settings yet — publishing today is framework-dependent (`--self-contained false`), which
  is fine for the Windows service script (assumes .NET 10 runtime present) but a Debian
  package needs an explicit decision: depend on the distro's/Microsoft's `dotnet-runtime`
  package, or publish self-contained + trimmed per architecture. Self-contained avoids a
  runtime-version dependency hell across Debian versions and matches what most `.deb`
  packages for .NET apps do in practice — recommend self-contained, linux-x64 (+ arm64 if
  needed), decided explicitly in this plan's Debian section below, not left implicit.

## Cross-cutting: versioning and update model

Both installers need one shared idea of "what version is this, and is it newer than what's
installed" — don't let Windows and Debian invent two different version schemes.

- Single source of truth: `<Version>` in `VideoForensics.WebApp.csproj` (currently
  `1.0.0`). CI/release tooling reads this once and feeds it to both packagers (`wix`'s
  `ProductVersion`, `.deb`'s `Version:` control field) so a release always ships the same
  version number on both platforms.
- Upgrade semantics: **in-place upgrade, not side-by-side install.** Installing a new
  version over an existing one must stop the running service, replace the binaries, run
  any pending DB migration (existing EF Core/SQLite migration path — confirm current
  migration mechanism before assuming `dotnet ef database update` vs. an in-app
  migrator), and restart the service — automatically, without the user manually
  uninstalling first. This is the actual "installer that supports updating" requirement,
  not just "an installer that also happens to work twice."
- Data safety on upgrade/uninstall: mirror the existing PowerShell script's contract —
  `%ProgramData%\VideoForensics` (Windows) / the Linux data dir (Debian) is **never**
  touched by upgrade or uninstall, only by an explicit "purge" path the user opts into.
  WiX and `.deb` postrm both need this spelled out explicitly (WiX: don't remove
  `CommonAppDataFolder` components; `.deb`: `postrm purge` only, never plain `remove`).

## Windows: MSI installer + EXE bootstrapper

1. **Tooling:** WiX Toolset v5 (current, actively maintained; MSBuild-integrated via the
   `WixToolset.Sdk` — fits this repo's all-MSBuild/`dotnet build` workflow better than
   hand-rolled `candle`/`light` calls or a third-party bundler), plus the
   `WixToolset.Bal.wixext` (Burn) extension for the bootstrapper. Two new projects, both
   under `deploy/windows/`, **not** under `src/` — packaging is not application code and
   shouldn't confuse the client/server split rules in `CLAUDE.md`:
   - `deploy/windows/VideoForensics.Installer.Wix/` — the `.msi` (`.wixproj`).
   - `deploy/windows/VideoForensics.Bootstrapper.Wix/` — the Burn `.exe` (`.wixproj`),
     `ProjectReference`-ing (WiX's package-reference mechanism, i.e. `<PackageGroupRef>`/
     harvested `.wixproj` output) the MSI project so it always bundles the MSI just built.
   Per this repo's global convention, both `.wixproj` files still need the
   `Microsoft.CodeAnalysis` package reference alongside whatever WiX packages they take.
2. **Publish input:** both are built from a self-contained `dotnet publish -r win-x64
   --self-contained true` of `VideoForensics.WebApp` (per the confirmed decision above) —
   no runtime-presence check needed in the bootstrapper because of this.
3. **What the MSI does:**
   - Harvests the self-contained publish output into `%ProgramFiles%\VideoForensics\`.
   - Registers the Windows Service via WiX's built-in `ServiceInstall`/`ServiceControl`
     elements (start type Automatic, account LocalSystem, failure recovery config) —
     replacing the manual `New-Service`/`sc.exe` calls in `install-service.ps1` with the
     MSI-native mechanism, so `msiexec` upgrades handle stop/replace/restart correctly
     without a bespoke custom action.
   - Creates the Windows Event Log source (matches what `install-service.ps1` already
     does) via a WiX util-extension element, not a custom action, if the extension
     supports it — avoids introducing custom action code that needs its own signing story.
   - Sets `MajorUpgrade` with `Schedule="afterInstallInitialize"` and a versioned
     `ProductCode`/stable `UpgradeCode`, so running the new MSI over an old install performs
     a proper major upgrade (stop old service, remove old files, install new, restart) —
     this is the mechanism that gives Windows its update support, WiX's equivalent of the
     PowerShell script's "stop, remove, reinstall" pattern but transactional and
     rollback-safe.
   - `%ProgramData%\VideoForensics` is referenced (for the Event Log source and any
     first-run seed file) but never marked for removal on uninstall.
4. **What the Burn `.exe` bootstrapper adds:**
   - Wraps the MSI as a single `.exe` payload with a themed UI (WiX's standard Burn
     `WixStandardBootstrapperApplication`) — the file most end users should download.
   - Because publish is self-contained, the bootstrapper needs no chained prerequisite
     package for the .NET runtime; it's kept anyway as the extensibility point for a
     future prerequisite (e.g. VC++ redistributable) without touching the MSI.
   - Inherits the MSI's upgrade behavior automatically — Burn re-runs the same MSI logic
     under the hood, so update support does not need separate bootstrapper-level logic.
5. **Signing:** the MSI, the bootstrapper `.exe`, and `VideoForensics.WebApp.exe` inside
   it should all be Authenticode-signed before distribution — flag this as a
   release-process step (cert + signing tool in CI), not something to hack around with
   `Set-ExecutionPolicy`-style workarounds for end users. Still open — see below.
6. **Retire vs. keep the PowerShell scripts:** keep `deploy/install-service.ps1` /
   `uninstall-service.ps1` for developer/CI use (quick iteration without building an MSI
   every time) but make the README clear the bootstrapper `.exe` is the supported
   end-user path once it exists, with the `.msi` available directly for
   `msiexec`/group-policy scenarios.

## Debian: `.deb` package

1. **Tooling:** build the `.deb` with `dotnet-deb` if it still adequately supports
   net10.0 (verify against current tooling first — don't assume), otherwise hand-roll the
   control/postinst/prerm/postrm files driven by a small script under
   `deploy/debian/` (this is the well-trodden path for .NET apps and gives full control
   over the systemd integration below without fighting a wrapper tool). New directory:
   `deploy/debian/` containing `control`, `postinst`, `prerm`, `postrm`, and a
   `videoforensics.service` systemd unit — no new `src/` project needed since this is
   packaging metadata, not code, though the packaging *script* itself should still live
   under `deploy/` alongside the existing PowerShell scripts for symmetry.
2. **Code change required first:** add `Microsoft.Extensions.Hosting.Systemd`'s
   `UseSystemd()` call to `Program.cs` right next to the existing `UseWindowsService()`
   call — same no-op-elsewhere idiom, needed so the app talks `sd_notify`/journal
   integration correctly under `systemd`, mirroring how `UseWindowsService()` was added
   for the SCM. Add the package reference to `VideoForensics.WebApp.csproj` alongside the
   existing `Microsoft.Extensions.Hosting.WindowsServices` reference.
3. **systemd unit (`videoforensics.service`):**
   - `Type=notify` (once `UseSystemd()` is wired up) or `Type=simple` if not; `Restart=on-failure`
     mirroring the Windows service's failure-recovery config for parity between platforms.
   - `User=`/`Group=` pinned to a dedicated `videoforensics` system user created by
     `postinst` (don't run the web app as root) — `postinst` must also `chown` the
     `CommonApplicationData`-equivalent data directory to that user on first install only
     (never on upgrade, to avoid clobbering permissions on an already-running data dir).
   - `WorkingDirectory=`/`ExecStart=` pointing at the install path (Debian convention:
     `/usr/lib/videoforensics/` or `/opt/videoforensics/` for the binaries — pick one and
     apply it consistently in `control`'s file list and the unit file).
4. **`postinst` responsibilities (this is where "supports updating" lives on Debian):**
   dpkg/apt already handles stopping the old service and replacing files on upgrade via
   the package's own file list diffing — `postinst` just needs to detect
   "upgrade vs. fresh install" (dpkg passes this as an argument: `configure <old-version>`
   is non-empty on upgrade) and:
   - Fresh install: create the system user, create the data directory, `systemctl enable
     --now videoforensics`.
   - Upgrade: run any pending DB migration, then `systemctl restart videoforensics` (not
     `enable --now` again — it's already enabled).
5. **`postrm` responsibilities:** on `remove`, stop/disable the service but leave the data
   directory untouched (matches the Windows uninstall contract above); only a `purge`
   action removes the data directory — spell this out explicitly since `postrm` gets
   called with different arguments for `remove` vs. `purge` and it's an easy place to
   accidentally delete user data.
6. **Packaging metadata (`control`):** `Depends:` should reflect the self-contained vs.
   framework-dependent decision from the cross-cutting section — if self-contained,
   `Depends:` only needs baseline glibc/libicu-type shared-library deps (check what
   `dotnet publish --self-contained` for net10.0 actually links against), not a
   `dotnet-runtime-10.0` package dependency.
7. **Distro scope:** confirm with the user which Debian/Ubuntu versions must be supported
   (affects glibc baseline if self-contained, and whether an `apt` repo/PPA is wanted for
   `apt upgrade` to "just find" new versions, vs. distributing a bare `.deb` file for
   manual `dpkg -i`) — this changes whether a hosted apt repository is in scope for this
   plan or is a separate follow-up.

## CI / release pipeline

- Single release job builds all artifacts from the same tagged commit/`<Version>`:
  `dotnet publish -r win-x64 --self-contained true` → WiX MSI build → Burn bootstrapper
  build (`.msi` + `.exe`); `dotnet publish -r linux-x64 --self-contained true` → `.deb`
  build.
- Output both artifacts plus their checksums as GitHub release assets — don't hand-carry
  binaries.
- This plan does not commit to auto-update (an in-app "check for updates" ping) — that is
  a distinct feature from "the installer supports being re-run to update" and should be
  scoped separately if wanted; flag this explicitly rather than silently expanding scope.

## Decisions confirmed by user (2026-09-18)

1. **Publish mode: self-contained** for both platforms (Windows `win-x64`, Linux
   `linux-x64`) — no runtime dependency on the target machine; `.deb` `Depends:` only
   needs baseline shared libraries, not `dotnet-runtime-10.0`.
2. **Windows output: both formats.**
   - A `.msi` built directly with WiX (the installable unit — Windows Service
     registration, major-upgrade, Event Log source all live here).
   - A `.exe` **Burn bootstrapper** (WiX v5's `WixToolset.Bal.wixext`) wrapping that MSI,
     so end users get a single double-clickable file. Since the app is self-contained,
     the bootstrapper does not need to chain-install the .NET runtime as a prerequisite —
     its main value here is the familiar single-`.exe` UX and room to add prerequisite
     checks later (e.g. VC++ redistributables) without changing the MSI.
   - Both are produced from every release; the MSI is also kept as a standalone artifact
     for `msiexec`/group-policy deployment scenarios.

## Open questions still to confirm with the user before implementation starts

1. Is a code-signing certificate available for the MSI/`.exe`? If not, ship unsigned for
   now and flag the SmartScreen/Defender friction this causes.
2. Target Debian/Ubuntu version floor, and whether a hosted apt repo is in scope now or
   later.
3. Confirm the actual current EF Core/SQLite migration invocation mechanism (needed by
   both `postinst` and the MSI's upgrade path) before either packaging script assumes how
   migrations run.

## Execution order

1. Add `UseSystemd()` + package reference to `VideoForensics.WebApp` (small, testable on
   its own, no-op on Windows — safe first step).
2. Debian packaging (`deploy/debian/`): control file, systemd unit, postinst/prerm/postrm,
   build script. Test on a real Debian/Ubuntu VM/container: fresh install, upgrade over an
   existing install, remove, purge.
3. Windows packaging: `deploy/windows/VideoForensics.Installer.Wix/` (MSI project,
   major-upgrade config, service registration), then
   `deploy/windows/VideoForensics.Bootstrapper.Wix/` (Burn bootstrapper wrapping it).
   Test: fresh install via both `.msi` and `.exe`, upgrade over an existing install
   (including one from the old PowerShell-script-based install, if that migration path
   matters), uninstall.
4. Wire both into the release CI pipeline.
5. Update `deploy/README.md` (or split into `deploy/windows/README.md` +
   `deploy/debian/README.md`) to document the new installer-first workflow, keeping the
   PowerShell scripts documented as the dev/CI-only path.

Per this repo's execution workflow rule, every step above is implementation work and gets
dispatched to Haiku subagents file-by-file/script-by-script once approved — this plan
itself is the Sonnet-level design output.
