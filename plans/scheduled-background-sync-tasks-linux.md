# Multi-provider-account scheduled tasks + Linux service

## Context

The Windows Service host (`VideoForensics.WebApp` via `UseWindowsService()`) currently has one background task at all: `DeviceHealthSyncService`, which polls RSSI on a single hardcoded 15-minute timer for whichever provider happens to be the unkeyed "active provider" in DI. Event sync and snapshot download are 100% manual, UI-button-triggered actions today (`Workflow.razor`, `CollectSnapshots.razor`) — there is no automated loop for either.

The user wants the service to run independent scheduled tasks **per provider account** (not just per provider type — a user can have multiple Ring accounts, each needs its own schedule): an event-sync task and a combined snapshot/RSSI task. They also want a Linux service with matching abilities.

Decisions locked in with the user:
- **Keyed DI per provider type** (`AddKeyedScoped<IEventAndConfigService>("Ring", ...)` etc.) — not a provider-factory abstraction, not one-process-per-provider.
- **Extend `VideoForensics.WebApp` cross-platform** (add `UseSystemd()` alongside `UseWindowsService()`) — not a separate Linux host project.
- **Per-provider-account, per-task-type interval config**, not a single global interval.

A known pre-existing issue surfaced during research: `RingDeviceDiscoveryService`/`RingEventAndConfigService` call `SessionProvider.GetSession()` (parameterless, "last account set" pointer), not the per-account overload. Running two Ring accounts' syncs truly concurrently would race on this. The plan below mitigates it (sequential processing within a provider type) rather than fixing it — a full account-context refactor of provider services is flagged as follow-up, out of scope here.

## Phase 0 — Schedule state storage (new DB table, not JSON)

Per-account interval/enabled/last-run state doesn't fit `appsettings.json` (accounts aren't known at deploy time) or `IAppSettingRepository`'s flat global-singleton pattern. Add a real table per CLAUDE.md's "design the schema first" rule:

- `src/data/common/data.common/Entities/ProviderAccountScheduleSetting.cs` — `Id`, `ProviderAccountId` (FK), `TaskType` (`EventSync`/`SnapshotAndRssi` enum), `Enabled`, `IntervalMinutes`, `LastRunUtc`, `LastSuccessUtc`, `LastErrorMessage`. Unique index `(ProviderAccountId, TaskType)`.
- `src/data/common/data.common/Contracts/IProviderAccountScheduleSettingRepository.cs` — `GetAsync`, `ListAllAsync` (used by the fan-out loop), `UpsertAsync`, `RecordRunAsync`.
- `src/data/database/data.database/Repositories/ProviderAccountScheduleSettingRepository.cs` + `Configurations/ProviderAccountScheduleSettingConfiguration.cs` (EF Core, mirrors `ProviderAccountRepository`).
- Add `DbSet<ProviderAccountScheduleSetting>` to `VideoForensicsDbContext.cs`; new EF Core migration.
- New `IOptionsMonitor<ScheduledTasksOptions>` section in `appsettings.json` (Phase 4) provides per-provider-type **defaults** used only to seed a new account's first `ProviderAccountScheduleSetting` row — the DB row is the source of truth after that.

## Phase 1 — Keyed DI registration

File: `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs`

- Add `AddVideoForensicsMultiProviderServices(IEnumerable<string> enabledProviders)` — a **sibling** to `AddVideoForensicsServerCore(activeProviderName)`, which stays exactly as-is (MAUI/UI interactive workflows still resolve the single unkeyed active provider; CLAUDE.md requires interface/DI shape not to change for the UI).
- For each enabled provider name, register the same four contracts (`IProviderAuthService`, `IDeviceDiscoveryService`, `IMediaDownloadService`, `IEventAndConfigService`, plus `IProviderHealthSource` where implemented — Ring only today) via `AddKeyedScoped<T>(providerName, factory)`.
- Reuse the existing per-provider construction lambdas already inlined at lines 97–182 and in `multiProviderAuthFactories` (lines 190–207) — refactor into one shared factory-builder used by the unkeyed registration, `IMultiProviderAuthService`, and the new keyed registrations, so there's one place per provider that builds it, not three.
- Unknown provider name → throw at startup (fail fast), same as today's `else` branch (line 183).
- `Program.cs` calls this new method after the existing `AddVideoForensicsServerCore` call, passing the provider list from config (`ScheduledTasks:EnabledProviders`).

## Phase 2 — Background services (fan out per account)

Two new `BackgroundService`s, following `DeviceHealthSyncService`'s existing pattern (`PeriodicTimer`, `IServiceScopeFactory`-scoped resolution, per-item try/catch isolation) extended one level — fan out over `(provider account)` pairs instead of health sources:

- `src/client/host/VideoForensics.Hosting/BackgroundServices/ProviderEventSyncService.cs` — calls `IEventAndConfigService.GetEventsAsync(...)` per account.
- `src/client/host/VideoForensics.Hosting/BackgroundServices/ProviderSnapshotSyncService.cs` — calls `IMediaDownloadService.DownloadSnapshotsAsync(...)` **and**, when that provider has a keyed `IProviderHealthSource`, also `FetchHealthAsync(...)` — one combined "snapshot/RSSI" task per account, matching the literal ask.

Design points:
- **Shared 1-minute driver tick**, not one `PeriodicTimer` per account: each tick lists all provider accounts + their `ProviderAccountScheduleSetting`, and only acts on ones where `UtcNow - LastRunUtc >= IntervalMinutes`. Gives independent effective per-account schedules without a timer-lifecycle-per-account reconciliation problem. Up to ~1 min jitter, acceptable at tens-of-minutes intervals.
- **Sequential within a provider type, parallel across provider types** — this is the session-race mitigation. Two Ring accounts run one after another; a Ring account and a Wyze account can run concurrently (different `SessionProvider`s). Call this out in the PR description as an accepted interim constraint.
- Failure isolation per account: a caught exception records `LastErrorMessage` via `RecordRunAsync` and moves to the next account; one account's failure never blocks another's.
- Reuses `IProviderApiBudgetGuard` exactly as `DeviceHealthSyncService` does today (still provider-name-scoped, not account-scoped — flag as follow-up, don't change its signature here).
- **`DeviceHealthSyncService` is left unchanged**, running only for the legacy unkeyed active-provider path — `ProviderSnapshotSyncService` is only registered when `ScheduledTasks:EnabledProviders` is non-empty, so existing single-provider installs see no behavior change. Document the resulting two-RSSI-code-paths state clearly in both files' doc comments; flag retiring `DeviceHealthSyncService` as follow-up once accounts have migrated.
- New `AddVideoForensicsProviderScheduledTasks()` registers `IProviderAccountScheduleSettingRepository` + both hosted services, called from `Program.cs` gated on `EnabledProviders.Any()`.

## Phase 3 — Cross-platform host

- `src/client/web/VideoForensics.WebApp/Program.cs:42` — branch on `OperatingSystem.IsWindows()` → `UseWindowsService(...)` (unchanged) vs `OperatingSystem.IsLinux()` → `UseSystemd()`.
- Same file: the `CommonApplicationData` hardcodes (Syncfusion license path ~line 31, logging dir ~66-68, data-protection keys ~113-114, `dbPath` ~366) need to route through the existing `PlatformDirectoryService` (`src/core/helpers/core.helpers/Platform/PlatformDirectoryService.cs`, already XDG-aware) instead — `CommonApplicationData` resolves to a non-writable path for a non-root Linux service user. This is required, not optional, for the Linux service to actually function.
- `VideoForensics.WebApp.csproj` — add `Microsoft.Extensions.Hosting.Systemd` (match the `10.0.11` pin already used for `WindowsServices`, adjusting if that exact patch isn't published).

## Phase 4 — Config schema

```json
"ScheduledTasks": {
  "EnabledProviders": [],
  "Ring":    { "EventSyncIntervalMinutes": 60, "SnapshotIntervalMinutes": 30, "Enabled": true },
  "Wyze":    { "EventSyncIntervalMinutes": 60, "SnapshotIntervalMinutes": 30, "Enabled": false },
  "Uniview": { "EventSyncIntervalMinutes": 60, "SnapshotIntervalMinutes": 30, "Enabled": false }
}
```

- New `ScheduledTasksOptions` bound via `IOptionsMonitor<T>` (not `IOptions<T>`) — these are long-lived `PeriodicTimer` loops, so a config-file edit should take effect on the next tick without a restart.
- These values are **seed defaults only**; `ProviderAccountScheduleSetting` DB rows are authoritative once created (Phase 0).
- Settings UI wiring (editing per-account schedules from `Settings.razor`) is a stretch goal, not required for v1.

## Phase 5 — Linux install scripts

Mirror `deploy/install-service.ps1`/`uninstall-service.ps1` (already read in full):

- `deploy/install-service.sh`: validate published output, copy to `/opt/videoforensics`, create a dedicated `videoforensics` system user (no login shell), create `/var/lib/videoforensics` (data) / `/var/log/videoforensics` (logs) owned by that user, write `/etc/systemd/system/videoforensics.service` (`Type=notify` to match `UseSystemd()`'s sd_notify integration, `Restart=on-failure` / `RestartSec=60` / `StartLimitIntervalSec=86400` mirroring the `.ps1`'s `sc.exe failure` recovery policy, `Environment=XDG_DATA_HOME=/var/lib/videoforensics` etc. so `PlatformDirectoryService` resolves to the right place for a system account), then `daemon-reload && enable --now`.
- `deploy/uninstall-service.sh`: stop/disable/remove the unit, `daemon-reload`, optional `--purge` to remove `/opt/videoforensics` (leave data dir untouched by default, matching the Windows script's behavior).
- Extend `deploy/README.md` with the Linux instructions.

## Phase 6 — Tests (xUnit + Moq, sibling `tests/`)

- `ProviderEventSyncServiceTests.cs`, `ProviderSnapshotSyncServiceTests.cs` (in existing `VideoForensics.Hosting.Tests`): due-schedule check, disabled-account skip, one-account-throws-others-still-run isolation, auth-restore-failure handling, budget-exceeded skip, seed-from-defaults-when-no-row, and **the multi-account-same-provider scenario** (two Ring accounts, independent intervals, verify only the due one runs; verify same-provider-type accounts are processed sequentially not concurrently).
- `VideoForensicsHostingExtensionsTests.cs`: unknown provider throws, two providers both resolvable via keyed services, DI-graph `ValidateOnBuild`/`ValidateScopes` smoke test for the new registrations (repo already uses this pattern to catch captive-dependency bugs).
- `ProviderAccountScheduleSettingRepositoryTests.cs` in the matching `data.database` tests project: CRUD + unique-constraint coverage (100% interface coverage per CLAUDE.md).

## Rollout

- `ScheduledTasks:EnabledProviders` ships **empty** by default — upgrading an existing install registers neither new hosted service, so nothing automated turns on silently; `DeviceHealthSyncService`'s existing behavior is unchanged. Opting a provider in is an explicit config change.
- Document the shared-budget-bucket-per-provider-type and sequential-same-provider-type limitations in the PR description.

## Follow-up (explicitly out of scope, call out but don't build)

1. Account-context refactor of `RingDeviceDiscoveryService`/`RingEventAndConfigService` (and Wyze/Uniview equivalents) so same-provider-type accounts can run truly concurrently instead of being serialized.
2. `IProviderApiBudgetGuard` scoped by account, not just provider name.
3. Retire `DeviceHealthSyncService` once installs have migrated to `ProviderSnapshotSyncService`.

## Execution

Per CLAUDE.md: main session designs, all file changes are dispatched to Haiku subagents in small batches (one service/file or a few related files at a time), escalating to Sonnet only if a subagent reports being blocked. After all phases land, ask the user before running the clean-rebuild-and-test gate (`dotnet clean` + `dotnet build` on the whole solution, fix every warning, then full `dotnet test`).

## Verification

- `dotnet build` the touched projects individually after each phase.
- `dotnet test` for `VideoForensics.Hosting.Tests` and the `data.database` test project after Phase 2/6.
- Manually run the WebApp locally with `ScheduledTasks:EnabledProviders: ["Ring"]` and a short `IntervalMinutes` (e.g. 1) against a test Ring account (or two, if available) to observe ticks in logs and `ProviderAccountScheduleSetting.LastRunUtc` advancing independently per account.
- On Linux (or WSL2 as a stand-in if no native Linux box is available), run `deploy/install-service.sh`, confirm `systemctl status videoforensics` is active, check logs land under `/var/log/videoforensics`, and confirm data persists under `/var/lib/videoforensics`.
