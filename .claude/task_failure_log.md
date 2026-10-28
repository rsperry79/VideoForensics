# Task failure log

Consolidated 2026-10-24 after the full gate passed on wip (bdffabe4: 7,041 tests, 0 failed, 2 skipped, 0 warnings).
Resolved entries were removed. Rules they produced are listed below.

## Open

2026-10-20 | Evidence.razor DI | IDownloadEventRepository not registered in the MAUI client host; no RemoteDownloadEventRepository exists | Evidence page breaks on MAUI | OPEN: add a remote repository or decline the page on MAUI
2026-10-20 | build | InternalsVisibleTo friend grants in Client.Core, Core.Helpers.Forensics, and Ring.Core carry PublicKey values from the earlier key | key scope undecided | OPEN: confirm which key the friend grants should use
2026-10-17 | ring real payload tests | zone_settings style integer vs entity Style string (JsonException) | production type decision | OPEN: decide the Style type; the real-payload fixture (doorbot-settings-sanitized.json) is in place
2026-10-24 | debug-launch-script | Resolve-Path used a wrong WebApp csproj name (real: VideoForensics.Client.WebApp.csproj); the prod deploy script has the same bad name | script fails to find the project | OPEN: fix both scripts to use the real name
2026-10-24 | debug-launch-script | exe name hardcoded as VideoForensics.WebApp.exe; AssemblyName is VideoForensics.Client.WebApp | service/exe name mismatch | OPEN: decide the service exe name
2026-10-24 | debug-launch-script | service path regex did not match unquoted paths with spaces | unverified | OPEN: match up to .exe lazily and test with a path that has spaces
2026-10-24 | webview2 | MAUI reports no suitable WebView2 Runtime (EdgeUpdate pv 155 vs installed 156; loader 0x80070002) | pv and native key set to 156 did not fix it; fixed-version runtime path is in fix/maui-local-password, now merged into wip | OPEN: verify on the Windows head
2026-10-24 | test flake | AccountLinkedWiringTests_Accounts.Accounts_Disposed_UnsubscribesFromAccountLinked (Ui.Shared bUnit) timed out under full-suite load once; passed alone and on rerun | flaky under load | OPEN: investigate the WaitForAssertion timing
2026-10-24 | git | new .razor/.cs files written with LF; repo files are CRLF | git warns on every commit | OPEN (low): normalise on next touch

## Rules (covered)

- Edit files with the Edit or Write tool, or a script file. Never pipe a bare python3 or a heredoc into bash (hangs on stdin, unbalanced quotes).
- Run `dotnet test` with a plain `--filter`. Ui.Shared uses MTP; filter by namespace or class substring (`FullyQualifiedName~QueryApi`), and confirm with `--list-tests`. File-name filters match zero tests.
- Don't use `--logger console`, `-p:DefaultItemExcludes`, or a build while a dev server holds the WebApp DLLs. Build to a scratch OutDir.
- Register every service an endpoint or component injects in its test base (test hosts, ScopeRail, QueryApi page base).
- Briefs for subagents must require a recorded failing run before implementation (TDD order).
- Never `git clean -X`. Match worktrees by `refs/heads/<branch>` after stripping `\r`, and run `git worktree list --porcelain` before removing one.
- Never kill processes in bulk (`taskkill /F` on dotnet.exe). Report a hung test host instead.
- Check original line endings with `git show` before editing a file. Write files with explicit CRLF when the repo file uses it.
- Stop a background shell's dotnet run with TaskStop, and don't assume PATH in background shells.
- Build with `-m:1` after a merge or a clean; parallel builds race on stale ref output and locked DLLs.
- Stale generated files with NUL bytes (obj folders) are deleted by explicit path; the next build regenerates them.
2026-10-24 19:51 | full suite on feat/finish-parts-1-2 | AesEncryption_SameSource_DecryptsEncryptedValue failed in the full run only (passed alone 3 of 3) | AesEncryptionTests deletes the shared salt file while the new tests run; salt creation also raced on first run | exclusive salt create (FileMode.CreateNew) and one xUnit collection for both AES classes; full suite 7054 passed | fixed
2026-10-24 21:25 | full suite on feat/preview-strips | Ring UnitTest.GetSnapshotTimestampTest failed once; passed alone and on a full rerun (7,083 total, 0 failed) | timing-dependent test, branch does not touch Ring | none; known flaky
2026-10-25T13:40 | build | npm run build:ts with && chain | npm script ran under PowerShell 5 parser, && rejected | replaced with scripts/build-ts.mjs (Node spawnSync)
2026-10-25T13:47 | test | npm run test:js chained build && vitest | && rejected under PowerShell npm shell | split into pretest:js npm hook
2026-10-25T13:47 | test | load-script new URL(import.meta.url) threw "URL must be of scheme file" | jsdom replaces global URL | use fileURLToPath + dirname
2026-10-25T14:10 | lint | batch node -e edit aborted on unmatched pattern in small-helpers.test.ts | multi-line pattern whitespace did not match | edited with exact-match Edit calls
2026-10-25 14:09 | build | SignalHistory.razor first compile | RZ1023 on <text> with attributes; CS0104 Device ambiguous (Core.Providers vs Data.Common.Entities) | Reserved Razor <text> tag used for SVG text; unaliased Device type | Built SVG labels via MarkupString helper; @using Device alias (covered by new tests passing)
2026-10-25 16:20 | windows-service | VideoForensics service exits on start: 'No frameworks were found' (.NET 10 x64) | HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\InstallLocation points at C:\Program Files (x86)\dotnet\ (only x86 runtimes there); x64 10.0.13 is under C:\Program Files\dotnet | OPEN: repair the x64 registry value (needs admin) or set machine DOTNET_ROOT, or publish self-contained via hotswap-service.ps1 -Build
2026-10-25 16:25 | windows-service | after setting x64 InstallLocation to C:\Program Files\dotnet\, service still fails with 'No frameworks were found'; direct launch of the exe fails the same way (exit -2147450730) | x64 key has sharedhost (10.0.13, correct) but no sharedfx subkey; host still resolves no framework | OPEN: reinstall .NET 10 x64 runtime, or self-contained redeploy via hotswap-service.ps1 -Build
2026-10-25 17:10 | publish | self-contained win-x64 publish still fails NETSDK1152 on tsconfig.json (Ui.Shared and WebApp src/ts) after 4 csproj attempts in worktree fix/publish-tsconfig-collision: Content Remove, None Remove, ResolvedFileToPublish Remove after ComputeResolvedFilesToPublishList, and BeforeTargets=_HandleFileConflictsForPublish | WebApp tsconfig.json is in ResolvedFileToPublish only; the Ui.Shared copy is also in it; first attempt was a bg publish killed mid-compile | OPEN: decide whether to exclude the ts folder from Ui.Shared's publish too, or set ErrorOnDuplicatePublishOutputFiles=false
2026-10-25 18:05 | sqlite-locking | regression test failed with SQLITE 5 'database is locked' on CREATE TABLE in probe, and 6 at SaveChanges with EF reader transaction | EF BeginTransactionAsync takes the write lock up front, so the reader was a writer | Use raw deferred read transaction (reader.BeginTransaction(deferred: true)) in SqliteLockingTests (fixed)
2026-10-25 18:07 | sqlite-locking | test build CS1739 on BeginTransactionAsync(deferred:) | that overload has no deferred parameter | Use synchronous BeginTransaction(deferred: true) (fixed)
2026-10-26 | media storage migration | dotnet build on VideoForensics.Hosting | wrong csproj path (guessed src\VideoForensics.Hosting.csproj) | used src/client/host/VideoForensics.Hosting/src/VideoForensics.Client.Hosting.csproj
2026-10-27 | ring-log-cleanup | recursive grep over src timed out (120s) | grep -rl walked build output | narrowed to *.cs with Grep tool
2026-10-27 | account-edit-gating | Ui.Shared test build CS1061 GetRequiredService on BunitServiceProvider | new page-gating test file missing the Microsoft.Extensions.DependencyInjection using | added the using (fixed)
2026-10-27 | account-edit-gating | 3 AccountLinkedWiringTests_Wizard tests: no registered service IProviderAccountEditAccess | AddAccountWizard now injects the new service; that test harness did not register it (error text names the new interface, so no wip baseline rerun was needed) | registered ProviderAccountEditAccessDouble in the harness (fixed)
2026-10-27 | test | LogEndpointsPipelineTests 30/32 failed at host startup | test host did not register LogFileDirectory; history route inferred it as body | registered in test StartAsync (commit 1d8dfe84); baseline on wip before fix: 30 failed, 2 passed; after fix 32 passed
