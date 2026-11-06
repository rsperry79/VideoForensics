# Task failure log

Pruned 2026-11-06. Resolved entries removed; history is in git. Rules that came out of them are in CLAUDE.md, `.claude/rules/*.md`, the code-workflow skill, or the list below.

## Open

- 2026-10-20 | Evidence.razor DI | IDownloadEventRepository not registered in the MAUI host; no remote repository | OPEN: add a remote repository or decline the page on MAUI
- 2026-10-20 | build | InternalsVisibleTo friend grants in Client.Core, Core.Helpers.Forensics and Ring.Core carry the earlier PublicKey | OPEN: decide which key the friend grants use
- 2026-10-17 | ring real payload tests | zone_settings style: integer vs entity string (JsonException) | OPEN: decide the Style type; fixture doorbot-settings-sanitized.json is in place
- 2026-10-24 | debug-launch and deploy scripts | wrong WebApp csproj name (real: VideoForensics.Client.WebApp.csproj); exe name hardcoded as VideoForensics.WebApp.exe; service path regex fails on unquoted paths with spaces | OPEN: fix both scripts, test with a path that has spaces
- 2026-10-24 | webview2 | MAUI reports no WebView2 Runtime (EdgeUpdate pv 155 vs installed 156; loader 0x80070002) | OPEN: verify on the Windows head; fixed-version runtime path is in merged fix/maui-local-password
- 2026-10-24 | test flake | AccountLinkedWiringTests Accounts_Disposed_UnsubscribesFromAccountLinked timed out under full-suite load | OPEN: investigate WaitForAssertion timing
- 2026-10-24 | git | new .razor/.cs files written LF; repo files CRLF | (covered) GitHub convention: git stores LF; see rules below
- 2026-10-25 | windows-service | .NET 10 x64 service exits 'No frameworks were found'; x64 InstallLocation points at x86 dotnet and the sharedfx subkey is missing | OPEN: reinstall the x64 runtime (admin), or self-contained redeploy via hotswap-service.ps1 -Build
- 2026-10-25 | publish | self-contained win-x64 publish fails NETSDK1152 on tsconfig.json (Ui.Shared and WebApp src/ts) | OPEN: exclude the ts folder from Ui.Shared publish, or set ErrorOnDuplicatePublishOutputFiles=false
- 2026-10-28 | stray worktree cleanup | .claude/worktrees/device-offline-settings not removed (Linux rm syntax; delete denied) | OPEN: user runs Remove-Item -LiteralPath <path> -Recurse -Force
- 2026-10-31 | logging | about 420 call sites still pass exception objects to the logger (credential path fixed 2026-11-03) | OPEN: sweep; csharp.md rule applies to new code
- 2026-10-31 | plan update | plan-update script and commit chained; the chain did not stop on failure | OPEN: run as separate steps and check each result
- 2026-10-31 | vitest baseline | small-helpers.test.ts imports media-viewer.js, which is not tracked on wip; layout-prefs tests fail on localStorage (also fail on origin/dev) | OPEN: separate fix
- 2026-11-02 | reason-lists red run | first dotnet test printed 'failed with 18 error(s)' before tests ran; not reproduced | OPEN: check whether it recurs
- 2026-11-02 | debug profile | launching the debug script through wt.exe ends the VS session at once | OPEN: Visual Studio debug launch untested; profile reverted to pwsh.exe
- 2026-11-03 | gate | Core.Helpers.Forensics KeyStorageFactoryTests: access denied on C:\ProgramData\VideoForensics\keys (ACL); also fails on origin/dev | OPEN: grant the test user write access or run as admin
- 2026-11-04 | ring salt tests | salt tests skipped until the path is injectable; the pinner pins Keys to ProgramData | OPEN: fix the pinner and keys pointer
- 2026-11-04 | database location | stale ProgramData database-location.json still on disk | OPEN: remove manually
- 2026-11-05 | cleanup | rm of F:/flagactions-green.log denied (outside repo) | OPEN: user deletes F:\flagactions-green.log

Watch (flaky under full-run load; rerun alone before calling it a regression): ReviewFilterPanel debounce and provider-burst tests, FolderPicker_Cancel, GetSnapshotTimestampTest, ServerLogsPage_ManyEntries, MaintenanceHoldTimer ArmSafetyTimer_AfterReserve, CaseRepositoryNumberingTests concurrent create, StorageDefaultPath_Tests (Keys status), one unnamed src/data/database/tests failure (2026-11-05).

## Rules (covered)

- Edit files with the Edit or Write tool, or a script file. Never pipe a bare python3 or a heredoc into bash (hangs on stdin, unbalanced quotes).
- Run `dotnet test` with a plain `--filter`. Ui.Shared uses MTP; filter by namespace or class substring (`FullyQualifiedName~QueryApi`), and confirm with `--list-tests`. File-name filters match zero tests.
- Don't use `--logger console`, `-p:DefaultItemExcludes`, or a build while a dev server holds the WebApp DLLs. Build to a scratch OutDir.
- Register every service an endpoint or component injects in its test base (test hosts, ScopeRail, QueryApi page base).
- Briefs for subagents must require a recorded failing run before implementation (TDD order).
- Never `git clean -X`. Match worktrees by `refs/heads/<branch>` after stripping `\r`, and run `git worktree list --porcelain` before removing one.
- Never kill processes in bulk (`taskkill /F` on dotnet.exe). Report a hung test host instead.
- Line endings are GitHub convention: git stores LF (`* text=auto`). Write LF; never convert a file to CRLF. The CRLF working copy on Windows is checkout only.
- Stop a background shell's dotnet run with TaskStop, and don't assume PATH in background shells.
- Build with `-m:1` after a merge or a clean; parallel builds race on stale ref output and locked DLLs.
- Stale generated files with NUL bytes (obj folders) are deleted by explicit path; the next build regenerates them.
