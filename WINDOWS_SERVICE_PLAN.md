# Run VideoForensics.WebApp as a Windows Service — Implementation Plan

## Goal

Let `VideoForensics.WebApp` run as an installed Windows Service in production, **without**
disturbing the dev workflow at all: `dotnet run`, F5/IDE debugging, and `preview_start` must
continue to work exactly as they do today, unchanged. Same binary, same `.csproj`, no separate
service-only project and no manual switching — the app detects its own hosting context at runtime.

## Core mechanism: `UseWindowsService()` is designed for exactly this

`Microsoft.Extensions.Hosting.WindowsServices`'s `UseWindowsService()` (called on the host builder)
checks `WindowsServiceHelpers.IsWindowsService()` internally and only actually swaps in the
Windows-Service-specific `IHostLifetime` (which talks to the Service Control Manager for start/stop/
pause signals instead of listening for Ctrl+C) when the process is genuinely running under the SCM.
In every other context — `dotnet run`, an IDE debugger, a console double-click — it's a complete
no-op and the app behaves exactly as it does today. This is the same idiom as `UseSystemd()` on
Linux, and it directly satisfies the "hot swappable for ease of dev work" requirement: there is
nothing to toggle, no build configuration to switch, no second project to keep in sync.

Confirmed clean starting point by reading the current codebase before planning further:
- No `SpecialFolder.ApplicationData` (user-profile `AppData`) usage anywhere in `WebApp` — everything
  persistent is already rooted at `%ProgramData%\VideoForensics\...` (DB, Data Protection keys, logs),
  which is exactly what a service account (no user profile) needs.
- No `Console.ReadLine`/`Console.ReadKey` or other interactive-console dependency in `WebApp`'s
  startup path — nothing will hang waiting for input under a session-less service.

## Required code changes

### 1. Add the package and call `UseWindowsService()`

- `src/client/web/VideoForensics.WebApp/VideoForensics.WebApp.csproj`: add
  `<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="..." />`
  (match the version already used for other `Microsoft.Extensions.*`/`Microsoft.AspNetCore.*`
  packages in this csproj for consistency).
- `Program.cs`: call `builder.Host.UseWindowsService(options => options.ServiceName = "VideoForensics");`
  early in the builder pipeline (right after `WebApplication.CreateBuilder(args)`, before the
  Kestrel/network-tier configuration that already runs there). This call is safe to make
  unconditionally on any OS — it's a documented no-op off Windows, matching this app's established
  platform-agnostic discipline (see the `OperatingSystem.IsWindows()` guards already used for DPAPI
  key protection) rather than needing its own conditional.

### 2. Consolidate logging into one shared library first — this needs to exist regardless of the service work

Checked before writing this: there is no shared diagnostic-logging library in this app today.
`VideoForensics.Core.Logging` (`src/core/core/core.logging`) is a different thing entirely — it's the
forensic *action audit trail* (`IActionLogger`/`ActionLogger`, the "MediaDownloaded" etc. entries
written to the `ActionLog` table), not `ILogger<T>` diagnostic logging. What actually exists for
diagnostic logging is two **copy-pasted** `FileLoggerProvider` classes — `src/client/maui/VideoForensics.MauiApp/Logging/FileLoggerProvider.cs`
and `src/client/VideoForensics/Logging/FileLoggerProvider.cs` — functionally identical (confirmed via
diff: only the namespace and minor style differ), and the MAUI one's own doc comment says outright
"copied from the console app's". `WebApp` has no custom provider at all today — just the ASP.NET Core
default console logger.

Before adding Event Log/syslog, fix this properly rather than bolting a third bespoke setup onto
`WebApp`'s `Program.cs`: add the shared logging extension to **`VideoForensics.Core.Logging`**
(`src/core/core/core.logging`), not `VideoForensics.Hosting`. It already exists, is already named for
exactly this purpose, and is already the right *shape* for it — a small, low-dependency project
(today: `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
and a reference to `Data.Common`) sitting low in the dependency graph, versus `Hosting`, which is a
heavier DI *composition root* that wires together the data layer and provider services — not the
natural home for a small, reusable, low-level cross-cutting piece like a logging provider. Confirmed
today's only consumer of `Core.Logging` is `VideoForensics.Data.Core` (for the existing `AddActionLogger()`
registration in `Contracts/IActionLogger.cs`/`Services/ActionLogger.cs`), which `Hosting` depends on -
so `Hosting`, and everything built on it (`WebApp`, `MauiApp`, the legacy console app), already has
`Core.Logging`'s assembly on its transitive build graph today, even without an explicit
`ProjectReference` anywhere else.

- Add a new file alongside the existing `Contracts`/`Services` folders in `Core.Logging`, e.g.
  `Providers/FileLoggerProvider.cs`, holding the one de-duplicated `FileLoggerProvider`/`FileLogger`
  implementation (move it here from whichever of the two current copies is cleaner, delete the other).
- Add a new extension method alongside the existing `AddActionLogger()` in
  `DependencyInjection/ServiceCollectionExtensions.cs` (same file, same static class) — something like
  `AddVideoForensicsLogging(this ILoggingBuilder logging, string logFilePath, bool enableEventLog = false, bool enableSyslog = false)` —
  containing:
  - The file provider registration (always).
  - The Windows Event Log provider registration, gated on both `enableEventLog` and
    `OperatingSystem.IsWindows()`.
  - The Linux syslog provider registration, gated on both `enableSyslog` and `OperatingSystem.IsLinux()`.
- `Core.Logging.csproj` needs the new package references added (`Microsoft.Extensions.Logging.EventLog`,
  `Serilog.Extensions.Logging`, `Serilog.Sinks.Syslog` — see below for why these specific ones).
- `WebApp`'s `Program.cs`, `MauiApp`'s `MauiProgram.cs`, and the legacy console app's `Program.cs` each
  call this one shared method instead of their own registration code (`WebApp` passing
  `enableEventLog: true`; MAUI/console leaving both flags off, since only `WebApp` runs unattended as
  a service). Add an explicit `ProjectReference` to `Core.Logging.csproj` from `MauiApp` and the legacy
  console app's `.csproj` even though the assembly is already transitively available via `Hosting` —
  directly consuming a library's types without an explicit reference to it is worth avoiding even when
  it happens to compile. This is a real, justified consolidation independent of the Windows Service
  work — it fixes existing duplication while adding the new capability in the same place, rather than
  compounding the duplication with a third copy.

### 3. Add Event Log (Windows) and syslog (Linux) logging, symmetrically

Under a service (Windows) or a headless daemon (Linux), there's no attached console — `Console`-based
log output (the default provider) is never seen by anyone. Confirmed nothing like this exists in the
codebase today (no `EventLog`, no `Serilog`, no syslog anywhere) — this is new, not a gap in an
existing setup. Add both platforms' native destinations as **additional providers alongside** the
existing `Microsoft.Extensions.Logging` pipeline, not as a wholesale logging-framework replacement —
this keeps the existing `appsettings.json` `Logging:LogLevel` filtering (already configured, see
`appsettings.json`/`appsettings.Development.json`) applying uniformly to every provider, old and new,
with no separate config format to maintain.

- **Windows — Event Log.** Official Microsoft package, a first-class `ILoggingBuilder` provider, no
  extra framework needed:
  ```csharp
  if (OperatingSystem.IsWindows())
  {
      builder.Logging.AddEventLog(settings => settings.SourceName = "VideoForensics");
  }
  ```
  `<PackageReference Include="Microsoft.Extensions.Logging.EventLog" Version="..." />`. The Event Log
  source (`"VideoForensics"`) must be created once with admin rights before first use (`New-EventLog`
  in the install script — the provider silently fails to write if the source doesn't exist yet).
  Creating event sources requires admin and should happen once at install time, not on every app
  start.

- **Linux — syslog.** No official Microsoft package exists for this (unlike Event Log), and hand-rolling
  a raw UDP/Unix-socket syslog sender is exactly the kind of infra code this project's own convention
  says to avoid when a maintained package exists (see `feedback_prefer_nugets` — check for a current,
  maintained package before writing infra code). `Serilog.Sinks.Syslog` is the standard, well-maintained
  choice, bridged into the *existing* `Microsoft.Extensions.Logging` pipeline as one more provider
  (via `Serilog.Extensions.Logging`'s `AddSerilog()` on `ILoggingBuilder`) rather than adopting
  `UseSerilog()`'s full host-level replacement — this is the smaller, lower-risk integration: Serilog
  becomes one provider among several, not a swap of the whole logging pipeline, so no other part of
  the app's logging behavior changes.
  ```csharp
  if (OperatingSystem.IsLinux())
  {
      var syslogLogger = new LoggerConfiguration()
          .WriteTo.LocalSyslog(appName: "VideoForensics")
          .CreateLogger();
      builder.Logging.AddSerilog(syslogLogger, dispose: true);
  }
  ```
  `<PackageReference Include="Serilog.Extensions.Logging" Version="..." />` and
  `<PackageReference Include="Serilog.Sinks.Syslog" Version="..." />`. `WriteTo.LocalSyslog(...)`
  writes to the local syslog daemon over a Unix domain socket — the standard case for a Linux service;
  confirm at implementation time whether this target machine's syslog setup (rsyslog/syslog-ng/journald
  compatibility layer) needs `LocalSyslog` vs `UdpSyslog`/`TcpSyslog` pointed at a remote collector, and
  adjust if a specific deployment target is known by then.
  Verify exact current package/method names against Serilog's docs at implementation time — API
  surface details here are illustrative, not gospel; confirm before writing the final code.

### 4. Fix the one relative-path bug found during this review

`src/client/core/VideoForensics.Client.Core/Tools/LocalRingSelfTestService.cs` (from the Ring
Self-Test feature added earlier) builds its output directory as a bare relative path:
```csharp
string outputDir = Path.Combine("SelfTesterResults", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
```
This resolves relative to the process's current working directory — harmless under `dotnet run`/an
IDE (working directory is the project's own folder), but a service's working directory is not
guaranteed to be anything in particular (often the executable's own folder with modern .NET service
hosting, but this must not be assumed). Fix by rooting it under the same `%ProgramData%\VideoForensics\`
base every other piece of persistent state in this app already uses, e.g.
`Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "SelfTesterResults", ...)`.

Before finalizing this plan's implementation, grep the full `WebApp`-reachable dependency graph once
more for any other bare relative `Path.Combine`/relative file access introduced since — do this as a
fresh check at implementation time, not from this snapshot, since code changes daily in this repo.

### 5. Verify graceful shutdown

`UseWindowsService()`'s lifetime already wires SCM stop signals into the standard
`IHostApplicationLifetime`/`IHostedService.StopAsync` shutdown path, so `DownloadProgressBroadcastService`
and any other `BackgroundService` in this app should already stop cleanly — no code change expected
here, but worth confirming by actually stopping the installed service and checking logs for a clean
shutdown sequence rather than an abrupt kill.

## Service installation (not part of the app itself)

New scripts under a new `deploy/` (or `scripts/`) folder at the repo root:

- **`install-service.ps1`** (requires admin): publishes (or accepts a pre-published output folder),
  creates the Event Log source (`New-EventLog -LogName Application -Source "VideoForensics"`, only if
  it doesn't already exist), and installs the service via `New-Service` pointed at the published
  `VideoForensics.WebApp.exe`, with:
  - `-StartupType Automatic`
  - Running as **LocalSystem** (simplest choice — the existing SQLite/ProgramData ACL setup grants
    the `BUILTIN\Users` group access, but `LocalSystem` bypasses ACL checks entirely and needs no
    extra grant; `NetworkService`/`LocalService` are *not* members of `BUILTIN\Users` by default and
    would need their own explicit ACL grant to work with the existing directory-creation code — avoid
    that complexity unless there's a specific reason to run as a lower-privilege account).
  - Failure recovery configured via `sc.exe failure` (restart on crash) immediately after `New-Service`,
    since `New-Service` itself has no recovery-options parameter.
- **`uninstall-service.ps1`** (requires admin): stops the service if running, removes it
  (`Remove-Service` on PS 6+, or `sc.exe delete` for broader compatibility), leaves data in
  `%ProgramData%\VideoForensics\` untouched (uninstalling the service must never delete user data).
- A short `deploy/README.md` documenting: how to publish (`dotnet publish -c Release -r win-x64
  --self-contained false` — framework-dependent is fine and smaller, since the target machine already
  has whatever .NET runtime this dev machine does), where the published output should live (e.g.
  `%ProgramFiles%\VideoForensics\`), and the install/uninstall commands.

## What this plan does *not* do (explicitly out of scope for now)

- No MSI/installer package — plain PowerShell scripts only, matching this repo's current tooling
  style (no existing installer infrastructure to extend).
- `MauiApp`, the legacy console app, and MCP are touched **only** for the logging-library
  consolidation (switching them onto the shared `FileLoggerProvider` instead of their own copies) —
  none of them get `UseWindowsService()`, Event Log, or syslog; that trio is `WebApp`-only, since
  it's the one host meant to run unattended/as a server.
- No multi-instance/clustering concerns — one service, one machine, matching the app's existing
  single-SQLite-file architecture.

## Execution order

1. **Agent A** — the shared logging library in `VideoForensics.Core.Logging`: de-duplicate
   `FileLoggerProvider` into one implementation, add the Event Log/syslog provider registration
   (both gated behind parameters so only `WebApp` actually turns them on), and switch `MauiApp` and
   the legacy console app onto the shared provider (adding an explicit `ProjectReference` to
   `Core.Logging.csproj` for both), deleting their own copies. Do this first — the other agents build
   on top of it existing.
2. **Agent B** (after A) — package references + `UseWindowsService()` in `WebApp`'s `Program.cs`,
   calling the new shared logging method with Event Log/syslog enabled (small, careful edit to a
   startup-critical file — brief the agent on the exact insertion point relative to the existing
   Kestrel/network-tier code, which must keep running before the host is built).
3. **Agent C** (parallel with B) — fix the `LocalRingSelfTestService.cs` relative-path bug, and do a
   fresh repo-wide grep for any other relative-path usage reachable from `WebApp`'s dependency graph
   at implementation time (don't rely solely on this plan's snapshot).
4. **Agent D** (parallel with B/C) — write `deploy/install-service.ps1`, `deploy/uninstall-service.ps1`,
   `deploy/README.md` (the install script now also creates the Event Log source).
5. **Main session** — full clean rebuild + test run (as with every change in this repo), then verify
   dev workflow is unaffected (`dotnet run`/`preview_start` still works identically) and that MAUI/the
   legacy console app still log to file correctly after the provider swap.
6. **Installing and starting a real Windows Service is a system-level, harder-to-reverse action**
   (registers with the SCM, runs under LocalSystem, persists across reboots) — do not run the install
   script without the user's explicit go-ahead at that point, even though writing the script itself is
   safe. Confirm before actually installing/starting the service on this machine.
