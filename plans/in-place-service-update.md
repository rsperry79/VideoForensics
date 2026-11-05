# In-place service update (dev hot-swap and release updates)

## Goal
The installed Windows service replaces its own binaries without a UAC prompt or a user-run installer.
One mechanism serves two sources: a dev build staged from VS, and a signed release package from GitHub.
Replaces `HotswapScriptRunner` (`runas`) and the `/passive` installer launch in `UpdateCheckService`.

## Flow
1. **Stage:** a package lands in `%ProgramData%\VideoForensics\updates\<version>\` with `manifest.json` written last.
   - Dev: post-build step or `DevWebAppLauncher` publishes there.
   - Release: `UpdateCheckService` downloads the release zip asset, verifies it, extracts to the same folder.
2. **Verify** before anything is touched:
   - Manifest lists every file with SHA-256; the manifest is signed.
   - Release: Authenticode signature on the exes (see `deploy/windows/codesign`), publisher must match the running exe.
   - Dev: unsigned allowed only when `Updates:AllowDevPackages` is true (off by default, never set by the installer).
   - Version must be greater than current, or equal with `AllowDevPackages`. Never allow downgrade except explicit rollback.
3. **Drain:** stop accepting new work, ask running syncs and downloads to checkpoint, write `updates\resume.json`
   (job ids, account, range and cursor). Reuse the existing sync resume state; do not add a parallel queue.
4. **Swap:** the service cannot overwrite its running exe.
   - Service exits with a non-zero code; SCM recovery (set by the installer) restarts it.
   - `Program.Main` runs an `UpdateApplier` before the host builds: if a verified staged package exists, back up the
     current files to `updates\previous\`, mirror the package into the install dir (same keep-list as the hotswap
     script: `Desktop`, cloudflared, ffmpeg, ffprobe, unins000), clear the staging folder.
   - Never touch `%ProgramData%\VideoForensics` data (DB, media, logs, keys).
5. **Resume:** on startup read `resume.json`, requeue the work, delete the file.
6. **Rollback:** if the new build does not report healthy within N seconds, or crashes twice on start, restore
   `previous\` and mark that version as bad so it is not re-staged.

## Triggers
- Folder watcher plus poll on `updates\` (works while the service is stopped: next start applies it).
- Optional named pipe `VideoForensics.Update` with an ACL limited to the interactive admin/dev user SID and SYSTEM:
  commands `status`, `drain-now`. No HTTP endpoint.
- UI: existing release-channel and update status stays; `AutoDownloadAndInstall` now means stage and apply.

## Security
- Service runs as SYSTEM; the staging folder must be writable only by SYSTEM and Administrators in release mode.
  Dev mode adds the dev user's write ACL through a one-time admin step, never in the shipped installer.
- No code runs from the staging folder before verification. Verify, then copy, then run.
- Zip extraction rejects path traversal and reparse points.

## One-time installer changes (admin, once)
- SCM recovery: restart on failure, 0 s delay.
- Staging folder with ACLs.
- Installer stops being the update path; keep it for first install and uninstall.

## Scheduler contract (for the scheduler revamp)
The update flow needs three things from the scheduler:
1. **Drain:** `DrainAsync(ct)` stops starting new runs, asks in-flight runs to checkpoint, and completes when idle.
   A `Resume`/`IsDraining` state lets a failed update cancel the drain.
2. **Persisted run state:** what was due, in progress and its cursor or range lives in the DB, not memory.
   Startup recovers interrupted runs exactly as it would after a crash, so no separate `resume.json` is needed.
3. **Missed-run policy:** runs that came due while the service was down run once on start or are skipped, per schedule.
With this, item 3 reduces to calling `DrainAsync` before exit.

## Debian
Same stage and verify flow, but swap needs a root service or systemd unit; scope separately after Windows.

## Implementation notes (from building items 1, 2 and 4)
- Apply runs at the start of the new process, not in a helper. The running exe and loaded dlls cannot be overwritten but
  can be renamed, so `UpdateApplier` renames locked files to `*.old-<guid>` and `DeleteLeftoverFiles` clears them on the next start.
- The process that applies still has the old files mapped, so it exits (code 1) and relies on SCM recovery to restart.
  **Item 6 (recovery action) must land before `Updates:Enabled` is switched on anywhere.** Option: have the service
  set its own recovery with `sc failure` at startup, since it runs as SYSTEM.
- Item 6 done: the service sets its own recovery at startup (`WindowsServiceRecoveryConfigurator`: restart actions plus
  `failureflag 1`, which was FALSE on an existing install) and locks `updates\` to SYSTEM and Administrators
  (`UpdatesFolderAcl`). The startup apply is skipped if recovery cannot be configured. The installer also sets `failureflag`.
- Dev mode needs the developer's SID added to the updates folder ACL (`UpdatesFolderAcl.Ensure(path, extraWriters)`); item 5 wires this.
- Item 5 done: `stage-update.ps1` stages a dev package (no admin); `StagedUpdateWatcher` in the service notices it, calls
  `IUpdateDrain` (no-op until the scheduler lands: item 3 replaces it) and exits abruptly with code 1. The exit must be
  abrupt: a graceful stop reports "stopped" and the service manager skips the recovery action. `enable-dev-updates.ps1`
  is the one-time admin step. `DevWebAppLauncher` no longer uses `runas`; it waits for the staged package to be consumed
  and then for health. `hotswap-service.ps1` is kept as the admin fallback.
- Everything is off by default behind `Updates:Enabled`; `Updates:AllowDevPackages` allows unsigned and same-version packages.
- Release mode needs an `IPackageSignatureVerifier` implementation; until then only dev packages verify.
- The staged installer matches the asset suffix `-update-win-x64.zip`; releases without it fall back to notify-only
  (item 11 must publish it).
- Still missing: the trigger that restarts a running service when a package is staged (drain, then exit), tied to item 3.

## Status
Implementation waits until the Media page rework plan is committed to `wip`. Item 3 also waits for the scheduler revamp.

## Work items
1. `UpdatePackage` (manifest model, hash and signature verify) with tests.
2. `UpdateApplier` (mirror with keep-list, backup, rollback) with tests on temp dirs.
3. Drain and `resume.json` in the sync pipeline.
4. `UpdateCheckService`: download zip asset instead of installer; publish a zip asset in the release workflow.
5. Dev stage step and `DevWebAppLauncher` change (stage, wait for health on the new version); retire `runas`.
6. Installer: recovery action, ACLs.
7. Named pipe (optional, last).
8. **DB migration safety (release blocker):** back up the DB before applying a package; migrations stay
   backward-compatible for one version, or rollback restores the backup. Rollback test covers a migrated schema.
9. **Version skew:** API version check between client/web UI and service; decide whether the `Desktop` folder
   updates with the package.
10. **Bundled tools:** cloudflared, ffmpeg, ffprobe either ship in the package with their own versions or are
    documented as frozen; remove them from the keep-list if shipped.
11. **Release pipeline:** CI builds, signs and publishes the zip asset and signed manifest. Needed before the
    app side can be tested end to end.
12. **Apply window:** `AutoDownloadAndInstall` waits for idle or a configured maintenance time; UI shows an
    "updating" state for remote and tunnel users.
13. **Failure handling:** disk-space preflight, retry on locked files (antivirus), failed updates surfaced in
    UI and logs, bad version remembered so it is not re-staged.
14. **Downgrade protection and key rotation:** tests that a signed older release is rejected; documented
    signing-key rotation path.
15. **End-to-end test on a VM with a real service:** swap, restart, resume, rollback.
16. **Housekeeping:** update the README "No Auto-Update" section, decide the fate of `hotswap-service.ps1`,
    note the breaking change in CLAUDE.md and the commit, commit this plan on `wip`.

## Decisions needed
- Package signing: Authenticode only, or an additional detached manifest signature (key stored in CI)?
- Does release publish a zip asset alongside the installer, or replace it?
