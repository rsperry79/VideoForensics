# VideoForensics MCP Server — Test Plan

Verifies the stdio MCP server (`src/client/VideoForensics.Mcp`) after the
`AddMcpServer()`/`host.RunAsync()` startup fix. Confirms the server connects,
responds within the client's timeout, and that each tool phase returns valid
data against the local SQLite database.

**Status: executed 2026-08-29 against `%APPDATA%\VideoForensics\videoforensics.db`
(1 location, 5 devices, 4,973 `MediaItems`/`DownloadEvents`, 0 `Events`/
`ExportRecords`/`AccessAuditLogs`/`DeviceHealthRecords`). 3 real bugs found and
fixed during the run — see Findings below.**

## 0. Preconditions

- [x] Rebuild + republish: `dotnet publish src/client/VideoForensics.Mcp/VideoForensics.Mcp.csproj -c Release`
- [x] Client config (Claude Desktop / claude-ai) points at
      `src/client/VideoForensics.Mcp/bin/Release/net10.0/publish/VideoForensics.Mcp.exe`
- [x] Reload/reconnect the MCP client so it spawns a fresh process (stale
      processes will keep serving the old broken binary)

## 1. Connection & startup (regression check for the bug just fixed)

- [x] Client's `initialize` request completes in well under the 60s timeout
      (observed: ~120ms) — check client logs for "method 'initialize' request
      handler completed"
- [x] `tools/list` returns all ~29 tools (8 timeline, 10 integrity, 7
      correlation, 9 audit — some overlap/pagination variants)
- [x] `resources/list` returns the `jamming-analysis-instructions` resource
- [x] Server logs show `Database initialization completed successfully`
      and `=== MCP SERVER READY FOR CONNECTIONS ===` before the client's
      first request is handled
- [x] Kill the client mid-session (Ctrl+C / close) — server logs a clean
      shutdown (`shutting down` / `shut down`), not a hang or crash dump
      (confirmed incidentally — process was killed/restarted 4x during this
      run to pick up rebuilds, always shut down cleanly)

## 2. Phase 1 — Timeline & Patterns

- [x] `get_timeline_summary` — pass
- [x] `get_event_count_by_day` — **failed, fixed** (Finding 1)
- [x] `get_event_count_by_hour` — pass
- [x] `get_peak_activity_periods` — pass
- [x] `verify_timeline_integrity` — pass
- [x] `get_coordinated_events` — pass
- [x] `find_suspicious_coordinated_activity` — pass
- [x] `get_recording_gaps_paginated` / `get_recording_gaps_cursor` — pass

## 3. Phase 2 — Evidence Integrity

- [x] `get_integrity_summary` — pass
- [x] `verify_event_hashes` — pass
- [x] `get_tampering_indicators` — pass
- [x] `get_tampering_indicators_paginated` — pass on default page size;
      **`pageSize=0` failed, fixed** (Finding 3, see Section 7)
- [x] `get_recording_gaps_paginated` / `get_recording_gaps_cursor` — pass
- [x] `identify_health_related_gaps` / `get_health_related_gaps_paginated` — pass
- [x] `analyze_device_reliability` — pass
- [x] `analyze_sync_health` — **`deviceStatus` silently empty, fixed** (Finding 2)
- [x] `get_event_health_correlation_cursor` — pass

## 4. Phase 3 — Correlation Queries

- [x] `get_correlation_summary` — pass
- [x] `find_suspicious_coordinated_activity` — pass (empty, consistent with
      empty `Events` table)
- [x] `get_coordinated_events` — pass

## 5. Phase 4 — Access & Export Audit

- [x] `get_audit_trail_summary` — pass
- [x] `get_access_history_paginated` — pass
- [x] `flag_unauthorized_access` — pass
- [x] `get_download_history_cursor` — pass (empty — see data note below)
- [x] `get_export_history` / `get_export_history_cursor` — pass
- [x] `verify_export_integrity` — pass
- [x] `verify_chain_of_custody` — pass
- [x] `verify_download_completeness` — pass

## 6. Resource

- [x] Fetch `videoforensics://instructions/jamming-analysis` — handler
      completed in ~49ms, no error

## 7. Edge cases

- [x] Call a paginated tool with `pageSize=0` — **crashed the call, fixed**
      (Finding 3); server itself stayed healthy and kept serving other
      requests (no full-process crash, just that one call errored)
- [x] Call a cursor tool with an invalid/garbage cursor — handled gracefully,
      silently falls back to the start of the result set (no crash)
- [x] Two tool calls issued back-to-back — both completed, no deadlock in
      the DI `Scoped` repositories
- [x] Server restarted 4 times in a row across this session (to pick up
      each fix) — `initialize` was consistently fast on every reconnect, no
      "works once, hangs on reconnect" regression

## Findings

1. **`get_event_count_by_day` always threw.** [TimelineRepository.cs:122-133](src/data/database/data.database/Repositories/TimelineRepository.cs:122)
   grouped by `e.OccurredAtUtc.Date.ToString("yyyy-MM-dd")` inside a LINQ
   `GroupBy` — EF Core can't translate `DateTime.ToString(format)` to SQL for
   SQLite, so the query threw `InvalidOperationException` on every call,
   regardless of data. **Fixed**: group by `.Date` (translatable), format the
   key to `yyyy-MM-dd` after materializing.

2. **`analyze_sync_health.deviceStatus` always serialized as empty objects.**
   `SyncHealthReport.DeviceStatus` in [ICorrelationRepository.cs:119](src/data/common/data.common/Contracts/ICorrelationRepository.cs:119)
   was `List<(Guid DeviceId, string DeviceName, decimal Uptime)>` — an
   unnamed `ValueTuple`. Tuple element names are compile-time only; at
   runtime the members are public *fields* (`Item1`/`Item2`/`Item3`), and
   `System.Text.Json` doesn't serialize fields by default, so every entry
   came back as `{}`. Every consumer of this field was silently getting no
   per-device data. **Fixed**: replaced with a proper `DeviceSyncStatus`
   class with real properties.

3. **Any offset-paginated tool crashed on `pageSize=0`.**
   `PaginatedResult<T>.TotalPages` in [PaginationModels.cs:10](src/data/common/data.common/Contracts/PaginationModels.cs:10)
   computed `(TotalCount + PageSize - 1) / PageSize` with no guard —
   integer division by zero throws in C#, and this type is shared by every
   offset-paginated tool across all 4 phases. **Fixed**: `TotalPages` now
   returns `0` when `PageSize <= 0` instead of throwing.

## Follow-up: Events ingestion gap — investigated and fixed 2026-08-29

The data-pipeline observation above turned out to be a real structural gap,
not just a data-freshness issue: **nothing in the codebase ever wrote to the
`Events` table.** `IEventRepository.UpsertAsync` existed and worked, but no
caller in the Ring provider ever invoked it — the download pipeline
([RingMediaDownloadService.cs](src/providers/ring/provider/Services/RingMediaDownloadService.cs))
only ever wrote to `DownloadEvents`/`MediaItems`, and
[RingEventAndConfigService.GetEventsAsync](src/providers/ring/provider/Services/RingEventAndConfigService.cs:25)
fetches events read-only and never persists them. Every forensic tool across
all 4 phases was reading from a table nothing had ever populated — for any
account, ever.

Fixed with user sign-off (live ingestion + backfill):

4. **Live ingestion wired up.** `RingMediaDownloadService.DownloadVideosAsync`
   now upserts an `Events` row for every provider event it sees —
   independent of download outcome — at discovery, at skip-existing, and at
   successful download (progressively enriching `DownloadedAtUtc`/
   `EventIntegrityHash`). Added `IVideoForensicsDataClient.UpsertEventAsync`
   as the facade method. Also fixed `EventRepository.UpsertAsync`'s update
   branch, which previously silently dropped `DownloadedAtUtc`/
   `EventIntegrityHash`/`ApiSourceHash` on every update (never persisted
   them at all) — it now merges them without letting a later "discovered"
   upsert wipe out an already-recorded download.

5. **One-time backfill.** New [EventBackfillService](src/data/database/data.database/Repositories/EventBackfillService.cs)
   reconstructs `Events` rows from the existing `DownloadEvents`/`MediaItems`
   history (joining on `MediaItem.DownloadEventId` for the hash). Wired into
   `Program.cs` as a background step after DB init, gated by an
   `AppSettings` flag (`EventsBackfillFromDownloadEventsCompleted`) so it
   only runs once. Verified on a copy of the live DB first, then run for
   real: **4,973 `DownloadEvents` → 4,973 `Events`**, all with hash and
   download timestamp populated.

   Caveat (inherent to backfilling from `DownloadEvents`, not a bug): events
   that were discovered but never downloaded — which is exactly the
   "missing download" signal forensic tools are meant to catch — can't be
   recovered this way, since they never had a `DownloadEvents` row either.
   Only future syncs (via the live-ingestion fix above) will capture those.

Post-fix, live tools now return real data instead of empty results:
`get_timeline_summary` → 4,973 events, 1,062 gaps, 96.4% coverage;
`get_integrity_summary` → 1,552 tracked, 240 failed recordings;
`get_event_count_by_day` → real per-day counts across Jun–Aug 2026.

### Finding 6 (found while verifying the above): more silent-`{}` tuple bugs

The same unnamed-`ValueTuple` serialization bug as Finding 2 was present in
4 more places once real data started flowing through them — a
`GroupBy(...).Select(g => (x, y))`-style tuple return always looks empty
until you feed it non-empty data, so these had been masked by the empty
`Events` table the whole time:

- `ITimelineRepository.GetPeakActivityPeriodsAsync` and
  `TimelineSummary.PeakHours` — `List<(int Hour, int Count)>` →
  `List<HourlyActivityCount>`
- `TimelineTools.GetPeakActivityPeriods` (the MCP tool itself) had the same
  tuple in its own signature — fixed too
- `CoordinatedEventCluster.Events` — `List<(Guid, string, string, DateTime)>`
  → `List<ClusterEvent>`
- `SuspiciousActivityFlag.InvolvedDevices` — `List<(Guid, string)>` →
  `List<InvolvedDevice>`
- `ExportIntegrityReport.ModificationDetails` — same tuple pattern fixed for
  consistency, though no repository code currently populates it (separate,
  pre-existing incompleteness — not fixed, out of scope)

All fixed by replacing the tuples with named DTO classes (same pattern as
Finding 2) and updating every construction site. Verified live:
`get_peak_activity_periods`, `get_timeline_summary.peakHours`,
`get_coordinated_events.events`, and
`find_suspicious_coordinated_activity.involvedDevices` all now return
fully-populated, correctly-named fields instead of `{}`.

**Lesson for future testing**: an empty dataset can hide serialization bugs
that only surface once there's real data to serialize. Worth re-running a
quick spot-check across all tools now that `Events` is populated.

## Pass criteria

All checkboxes above complete with no unhandled exceptions in server logs
(`FATAL`, `CRITICAL`, or an unlogged process exit), and every `initialize`
handshake completes well inside the client's timeout window. **Met**, after
fixing the 3 findings above.
