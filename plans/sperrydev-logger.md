# Plan: move Logging and the CLI log viewer to SperryDev.Logger

## Decisions
- Data coupling: only file, pipe, and console logging move. `ActionLogger` and `IActionLogger` stay in VideoForensics because they depend on `VideoForensics.Data.Common`.
- Viewer scope: only the CLI viewer (`src/utils/logviewer`) moves. `ILogViewerService`, the Hosting Local/Remote services, `ServerLogs.razor`, and the WebApp endpoints stay and reference the new package.
- Location: new repo at `F:\SperryDev.Logger`. Local NuGet folder feed at `F:\LocalNuget`.

## Current state
- `src/core/logging` (`VideoForensics.Core.Logging`, namespace `VideoForensics.Core.Logging.*`) has 35 consumers across Hosting, MAUI, WebApp, and `Data.Core` via ProjectReference.
- `src/core/logging` also has a ProjectReference to `src/data/common`. That is the coupling being cut.
- `src/utils/logviewer` (`VideoForensics.Utils.LogViewer`, exe) references Core.Logging.
- The repo's `nuget.config` lists only nuget.org. No local feed exists.

## Progress
- Step 1 done: `F:\SperryDev.Logger` on `main` (commit `2bd0658`): `SperryDev.Logger.slnx`, `Directory.Build.props`, `version.json` at 0.1.0. `F:\LocalNuget` created.
- Findings for steps 3-4: `Data.Common` is used only by `Contracts/IActionLogger.cs`, `Services/ActionLogger.cs`, and `tests/TestHelpers.cs` (a using with no visible use). `InMemoryLogBufferProviderTests.cs` only names a namespace string. `LogLevelSource`, `LogFileDirectory`, and `ServiceCollectionExtensions` have no Data.Common refs, so the coupling cut is clean.
- Step 2 done: red test project (`tests/SperryDev.Logger.Tests`) built, failing on missing project as expected.
- Step 3 done: source moved into `src/SperryDev.Logger`, `AddActionLogger` removed from DI (stays in VideoForensics). `TestHelpers.cs` removed from the new test project (builds `ActionLogEntry`, stays with ActionLoggerTests). `global.json` opts into the MTP runner (same as VideoForensics). Tests: 118 passed, 0 failed. Committed `ecd8cae` on `main`.
- Step 4 done: viewer moved to `src/SperryDev.Logger.Viewer` (commit `e001f6a` on `main`). Solution entry fixed by hand because `dotnet sln add` failed on a name clash.
- Step 5 done: `SperryDev.Logger.0.1.0.nupkg` in `F:\LocalNuget`. Both feeds are in VideoForensics `nuget.config`: `F:\LocalNuget` and GitHub Packages (`rsperry79`, auth via `GITHUB_TOKEN`).
- Step 6 done: residual `VideoForensics.Core.Logging` keeps only `ActionLogger`, `IActionLogger`, and `AddActionLogger`, and references the package. Hosting, MAUI, WebApp, and Data.Core reference the package. Data.Core keeps its project reference because it uses `IActionLogger`. 12 .cs files repointed (usings only).
- Step 7 done: logviewer removed from `VideoForensics.sln`. CI publish steps for the viewer removed from `build-windows.yml` and `release-installers.yml`. The installer's Log Viewer entry is guarded by `#if LogViewerExeExists`, so it is simply omitted now.
- Step 8 done: full `VideoForensics.sln` build, 0 errors. Tests pass: Hosting 1263, WebApp 1127, Core.Api.Contracts 45, Data.Core 261, Core.Logging 9. SperryDev.Logger tests: 118.
- Open, needs the user: (a) publish `SperryDev.Logger` 0.1.0 to GitHub Packages so CI can restore it. This is an outward publish, so it is not done. (b) `deploy/windows/VideoForensics.iss` comment and `ToolsReadme.txt` still describe the viewer as shipped with VideoForensics.
- Step 9: commits made on `main` (SperryDev.Logger) and on `feature/sperrydev-logger` (VideoForensics). Not pushed.

## Steps
1. Scaffold the repo. Create `F:\SperryDev.Logger` with `git init` on `main`, a `SperryDev.Logger.sln`, and `src/` and `tests/` folders. Root namespace `SperryDev.Logger`. Add `Directory.Build.props` with package metadata and `version.json` (start at 0.1.0).
2. Failing tests first (tdd). Move the logging tests for file, pipe, console, in-memory buffer, redactor, crash logger, and history reader. Leave the ActionLogger tests in VideoForensics.
3. Move the code. Copy `Providers/`, `Services/` (minus `ActionLogger.cs`), and `DependencyInjection/` into the new repo. Rename namespaces `VideoForensics.Core.Logging` to `SperryDev.Logger`. Remove the `Data.Common` ProjectReference. Check whether `LogLevelSource`, `LogFileDirectory`, or `ServiceCollectionExtensions` use Data.Common types; if they do, cut those references before the move. `ActionLogger.cs` and `Contracts/IActionLogger.cs` stay in VideoForensics under a `VideoForensics.*` namespace in a project that still references the new package.
4. Move the viewer. Move `src/utils/logviewer` into the new repo as `SperryDev.Logger.Viewer`, with its ProjectReference pointing at the new logging package.
5. Pack and feed. `dotnet pack` (Release) into `F:\LocalNuget`. Add `F:\LocalNuget` as a local source in VideoForensics `nuget.config`, keeping nuget.org.
6. Repoint VideoForensics. Replace the Core.Logging ProjectReferences in Hosting, MAUI, WebApp, and `Data.Core` with `PackageReference SperryDev.Logger`. Sweep `using VideoForensics.Core.Logging.*` to `SperryDev.Logger.*`.
7. Update the solution and CI. Remove moved projects from `VideoForensics.sln` and `VideoForensics.CI.slnf`. Check `.github/workflows`, `version.json`, and pack steps in `Directory.Build.targets` for references to the moved projects.
8. Verify. Lite gate on the new repo and each touched VideoForensics `.csproj`. Then one full build of the VideoForensics solution to confirm restore from the local feed.
9. Commit. Commit the new repo on `main`. In VideoForensics, branch from `wip` in a worktree under `F:/vf-worktrees/sperrydev-logger`, following the batch handoff in the code-workflow SOP.

## Risks
- Clean separation: the `ActionLogger` split needs a decision in step 3 if a helper it uses is shared.
- Local feed versioning: every change needs a version bump and repack, or consumers keep the old package.
- Blazor/UI consumers of the viewer are unaffected and stay in VideoForensics.
