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
