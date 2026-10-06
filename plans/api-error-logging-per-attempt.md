# Persist Provider API Errors (Status Code + Response Body) per Event

## Context

Today, when a Ring download fails, only a free-text `DownloadEvent.ErrorMessage` (a humanized .NET exception type + `ex.Message`) is persisted — and `DownloadEvent` is upserted per `(DeviceId, ProviderEventId)`, so it only ever holds the *latest* attempt's state, overwriting prior attempts. The actual HTTP status code and raw response body Ring returned *are* captured today (via `ApiRawLogger.OnRawResponse`, see [ApiRawLogger.cs](src/providers/ring/core/ApiRawLogger.cs)), but that stream only reaches a text log file ([Program.cs:107-108](src/client/VideoForensics/Program.cs#L107-L108)) — never the database. This makes it impossible to later answer "what exactly did Ring say, every time this event failed?" or to distinguish *why* an event is unavailable — most importantly, the case where a recording **was** successfully downloaded before but Ring now 404s it (deleted on Ring's side) versus one that was simply never available.

This plan adds a new one-`Event`-to-many error-log table that records every failed attempt's status code, (truncated) response body, and a classified error category — including a dedicated "recording existed, now gone" category — for both video and snapshot download failures.

## 1. New entity + one-to-many relationship

New entity `ProviderApiErrorLog` (`src/data/common/data.common/Entities/ProviderApiErrorLog.cs`), following this codebase's existing convention of plain Guid "soft" foreign keys (no EF navigation properties — see `Event`/`DownloadEvent`/`MediaItem` for the pattern):

```csharp
public class ProviderApiErrorLog
{
    public Guid Id { get; set; }
    public Guid? EventId { get; set; }        // Event.Id when known (video downloads); null for snapshots (no Event row exists for those, per the backup-export work)
    public Guid DeviceId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestUrl { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }  // truncated to ~4000 chars at write time
    public string? ExceptionType { get; set; }
    public string? ErrorMessage { get; set; }
    public required string ErrorCategory { get; set; }
}
```

`EventId` is many-to-one against `Event.Id` (one Event can accumulate many error-log rows across retries) — this is deliberately **not** stored on `DownloadEvent` (which only ever holds the latest attempt) so full history survives every retry.

**`ErrorCategory` values** (plain string, not a DB enum — matches `RecordingStatus`/`SyncStatus` string-based conventions elsewhere): `RateLimited`, `RecordingNotFound`, `RecordingDeletedAfterDownload`, `DownloadFailed`, `UnexpectedStatus`, `Cancelled`, `Other`.

`RecordingDeletedAfterDownload` is the "was there, someone deleted it" case: in the video-download catch block, `existingRecord` (a `DownloadEvent?` already fetched earlier in the method, see [RingMediaDownloadService.cs:245](src/providers/ring/provider/Services/RingMediaDownloadService.cs#L245)) tells us whether this event previously downloaded successfully (`existingRecord?.Success == true`). If a `DeviceUnknownException` (Ring 404) is now caught for that same event, classify as `RecordingDeletedAfterDownload`; otherwise (never successfully downloaded before) classify as `RecordingNotFound`.

## 2. EF configuration + migration

New `ProviderApiErrorLogConfiguration : IEntityTypeConfiguration<ProviderApiErrorLog>` (`src/data/database/data.database/Configurations/`), mirroring [DownloadEventConfiguration.cs](src/data/database/data.database/Configurations/DownloadEventConfiguration.cs) and [ProviderApiCallRecordConfiguration.cs](src/data/database/data.database/Configurations/ProviderApiCallRecordConfiguration.cs):
- `HasKey(e => e.Id)`
- `RequestUrl` maxlength 2048, `ExceptionType`/`ErrorCategory` maxlength 128, `ErrorMessage` maxlength 1024, `ResponseBody` no maxlength (SQLite `TEXT`, already bounded to ~4000 chars at write time — see §4)
- `HasIndex(e => e.EventId)` — the one-to-many lookup path
- `HasIndex(e => new { e.DeviceId, e.OccurredAtUtc })` — for the snapshot case where `EventId` is null

Register the new `DbSet<ProviderApiErrorLog>` on `VideoForensicsDbContext`, then generate the migration the same way [20260904040432_AddProviderApiCallLog.cs](src/data/database/sqlite/data.database.sqlite/Migrations/20260904040432_AddProviderApiCallLog.cs) was produced — run:
```bash
dotnet ef migrations add AddProviderApiErrorLog --project src/data/database/sqlite/data.database.sqlite --startup-project src/client/VideoForensics
```
(adjust `--startup-project` to whichever host project the repo's other migrations were generated against — check the most recent migration's context for the exact command/paths used). This auto-generates both the migration and the updated `.Designer.cs`/`VideoForensicsDbContextModelSnapshot.cs` — do **not** hand-author these, per the existing pattern being 100% tool-generated.

## 3. Repository + contract

New narrow contract `IProviderApiErrorLogRepository` (`src/data/common/data.common/Contracts/`), matching [IProviderApiCallLogRepository.cs](src/data/common/data.common/Contracts/IProviderApiCallLogRepository.cs)'s narrow-interface style:
```csharp
public interface IProviderApiErrorLogRepository
{
    Task RecordAsync(ProviderApiErrorLog entry, CancellationToken ct);
    Task<IReadOnlyList<ProviderApiErrorLog>> GetByEventIdAsync(Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<ProviderApiErrorLog>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);
}
```
Implementation `ProviderApiErrorLogRepository` (`src/data/database/data.database/Repositories/`) follows [ProviderApiCallLogRepository.cs](src/data/database/data.database/Repositories/ProviderApiCallLogRepository.cs)'s `IDbContextFactory<VideoForensicsDbContext>` + `await using var db = ...` pattern.

Add a pass-through method on `IVideoForensicsDataClient`/`VideoForensicsDataClient` (`src/data/core/data.core/`) — `Task RecordProviderApiErrorAsync(ProviderApiErrorLog entry, CancellationToken ct)` — so `RingMediaDownloadService` keeps going through the same facade it already uses for `UpsertEventAsync`/`RecordDownloadEventAsync`, rather than injecting the repository directly.

## 4. Carry status code + body from the HTTP layer into the exceptions

Per the exploration: `ApiRawLogger.Raise` has no correlation id and only reaches a log file; none of the exception types thrown by `HttpUtility.cs`/`Session.cs` carry a response body today (only `UnexpectedOutcomeException` carries a status code). Given `RingMediaDownloadService` downloads multiple files concurrently per device (`_maxConcurrentFileDownloads`), an ambient/thread-local "last response" would race — so thread the data explicitly through the exceptions instead:

- Add `public HttpStatusCode? StatusCode` and `public string? ResponseBody` properties to `DeviceUnknownException`, `DownloadFailedException`, and `ThrottledException` (`src/providers/ring/common/Exceptions/`); add `ResponseBody` to `UnexpectedOutcomeException` (it already has `ReturnedStatusCode`).
- At each throw site inside `HttpUtility.cs` (the four call sites already identified: `GetContents` ~line 242, `DownloadFile` ~line 600, `SendRequestWithExpectedStatusOutcome` ~line 638, `SendRequest` ~line 707), the response body is already read into a local variable before `ApiRawLogger.Raise` is called — pass that same variable (truncated to 4000 chars, `body.Length > 4000 ? body[..4000] : body`) and the status code into the exception constructor at whichever of these methods throws one of the four exception types above.

## 5. Wire into RingMediaDownloadService failure paths

**Video** ([RingMediaDownloadService.cs:500-544](src/providers/ring/provider/Services/RingMediaDownloadService.cs#L500-L544)): in the per-item catch block, after building `failedEvent`, also build and record a `ProviderApiErrorLog`:
- `EventId = eventDbId` (the `Guid` already returned by `UpsertEventRecordAsync` from this same session's backup-export work — reuse it, no new lookup needed)
- `DeviceId = deviceGuid`, `AttemptNumber = previousAttemptCount + 1`, `OccurredAtUtc = DateTime.UtcNow`
- `HttpStatusCode`/`ResponseBody` pulled from the exception when it's one of the four extended types (pattern-match `ex as DeviceUnknownException`, etc.), else null
- `ErrorCategory` per the classification in §1 (`ThrottledException` → `RateLimited`; `DeviceUnknownException` → `RecordingDeletedAfterDownload` if `existingRecord?.Success == true` else `RecordingNotFound`; `DownloadFailedException` → `DownloadFailed`; `UnexpectedOutcomeException` → `UnexpectedStatus`; else `Other`)
- `ExceptionType`/`ErrorMessage` mirror what's already built for `failedEvent.ErrorMessage`
- Call `_dataClient.RecordProviderApiErrorAsync(entry, CancellationToken.None)` in the same `try/catch(recordEx)` block already wrapping `RecordDownloadEventAsync`, so a logging failure here can't fail the download itself.

**Snapshot** ([RingMediaDownloadService.cs:826-834](src/providers/ring/provider/Services/RingMediaDownloadService.cs#L826-L834)): this method has one top-level catch (not a per-item loop), and no `Event` row exists for snapshots. In that catch:
- `EventId = null`, `DeviceId = deviceGuid` if it was already resolved (i.e. failure happened after `EnsureDeviceIdentityAsync` at line ~718; if it failed before that, skip recording — no device to attach it to)
- `AttemptNumber = 1` (snapshots aren't retried the way video events are)
- Classification limited to what's distinguishable at this single catch-all point: `ThrottledException` → `RateLimited`, `DeviceUnknownException` → `RecordingNotFound` (no "previously downloaded" signal available here the way video has `existingRecord`), else `Other`
- Same fire-and-forget `try/catch` guard around the record call so it can't fail the snapshot download's own error return path.

## Files touched

- `src/data/common/data.common/Entities/ProviderApiErrorLog.cs` (new)
- `src/data/database/data.database/Configurations/ProviderApiErrorLogConfiguration.cs` (new)
- `src/data/database/data.database/DbContext/VideoForensicsDbContext.cs` (add `DbSet`)
- New EF migration under `src/data/database/sqlite/data.database.sqlite/Migrations/` (generated via `dotnet ef migrations add`)
- `src/data/common/data.common/Contracts/IProviderApiErrorLogRepository.cs` (new)
- `src/data/database/data.database/Repositories/ProviderApiErrorLogRepository.cs` (new)
- `src/data/core/data.core/Contracts/IVideoForensicsDataClient.cs` + `src/data/core/data.core/Services/VideoForensicsDataClient.cs` (add `RecordProviderApiErrorAsync`)
- `src/providers/ring/common/Exceptions/DeviceUnknownException.cs`, `DownloadFailedException.cs`, `ThrottledException.cs`, `UnexpectedOutcomeException.cs` (add `StatusCode`/`ResponseBody` properties)
- `src/providers/ring/core/HttpUtility.cs` (populate the new exception properties at the four throw sites)
- `src/providers/ring/provider/Services/RingMediaDownloadService.cs` (both catch blocks per §5)
- DI registration for `IProviderApiErrorLogRepository` alongside the other repositories (`src/data/database/data.database/DependencyInjection/ServiceCollectionExtensions.cs`)
- Sibling `tests/` projects: repository test, and updated/new `RingMediaDownloadServiceTests.cs` cases for the `RecordingDeletedAfterDownload` vs `RecordingNotFound` classification

## Verification

- Unit tests: repository round-trip (`RecordAsync` + `GetByEventIdAsync` returns multiple rows for the same `EventId` across simulated retries); classification logic for `RecordingDeletedAfterDownload` (mock `existingRecord.Success = true`, throw `DeviceUnknownException`, assert category) vs `RecordingNotFound` (`existingRecord = null`).
- `dotnet build` the solution; run the ring provider test executable directly (`dotnet test` doesn't discover xUnit v3 tests in this repo — run the built `.exe` directly, as done for the prior backup-export feature).
- Manual: force a 404 against a real/mocked Ring event that was previously downloaded (delete the local file so a redownload is attempted, or use a known-deleted event id) and confirm a `ProviderApiErrorLog` row appears with `ErrorCategory = RecordingDeletedAfterDownload`, a populated `HttpStatusCode`, and a non-empty (truncated) `ResponseBody`.
