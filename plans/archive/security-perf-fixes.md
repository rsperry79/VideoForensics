# Fix remaining code-review findings (security, perf, god-class)

## Context

An earlier full-codebase review surfaced 8 findings; 2 (the MCP client/server-split violations)
are already fixed. This plan covers the remaining 6, informed by follow-up investigation into each
one's actual blast radius and feasibility:

- **Finding #3 (bearer token over plaintext HTTP on LAN): SKIPPED per your confirmation.**
  Investigation found there is no TLS infrastructure anywhere in this app today — no Kestrel cert
  binding, no self-signed cert generation, and `PairedDevice.PinnedCertificateFingerprint` is an
  unused schema placeholder, not implemented pinning. `ServerLocationResolver.cs`'s own doc comment
  already documents this as a deliberate tradeoff (LAN is the trust boundary; the bearer token is
  the real control). A real fix is a separate, multi-part feature (cert generation + Kestrel HTTPS
  + pinning validation), not a bug fix — out of scope here, left as-is.
- **Finding #4 (Ring credential key trivially re-derivable, non-Windows):** hardened in place —
  no OS keychain/libsecret integration exists in this repo to reach for, so a full fix is its own
  feature. This plan raises the bar within `AesEncryption`'s existing design instead.
- **Finding #5 (plaintext password to disk):** confirmed dead code (no production caller wires up
  `SanitizeClearTextPassword` today) and the sanitize step already correctly strips the plaintext
  field before rewriting the file. Hardened defensively anyway since it's public `ICredentialStore`
  API that could be wired up later.
- **Finding #6 (video fully buffered in memory):** confirmed safe to fix — no production caller
  needs in-memory bytes; the existing hash step already reads the finished file from disk.
- **Finding #7 (`EventRepository.ListAsync` untracked/unbounded):** confirmed one real caller
  (`GET /api/v1/events`) needs genuine pagination added; the other two callers
  (`BackupExportOrchestrator`) legitimately need the full table and get the `AsNoTracking()` win
  for free.
- **Finding #8 (`RingMediaDownloadService` god class):** confirmed 5 cleanly-isolated clusters with
  no/minimal shared-field dependencies, extractable via the exact constructor-injection-with-
  inline-fallback pattern this file already uses for `IMediaMetadataTagger`/`FfmpegMediaMetadataTagger`.

## Approach

### #4 — `src/providers/ring/auth/Implementations/AesEncryption.cs`

`DeriveKey()` currently does `PBKDF2("{machineId}:{userId}", salt: "RingVideos", 10,000 iters)` —
salt is a hardcoded literal (not a secret) and 10,000 iterations is well below current guidance.
Two independent, additive hardenings, no architecture change:
1. Raise `Iterations` to 600,000 (current OWASP baseline for PBKDF2-HMAC-SHA256).
2. Replace the hardcoded salt with a per-installation random 16-byte salt, generated once via
   `RandomNumberGenerator.GetBytes(16)` and persisted next to the credential store (e.g.
   `%ProgramData%\VideoForensics\ring-credential-salt.bin`), read back on subsequent runs. This
   removes the "key derivable from public info alone" flaw the finding named — an attacker now
   needs the salt file too, not just the machine GUID and username.
Document in the class's doc comment that full protection still requires OS-native secret storage
(keychain/libsecret) on non-Windows, which doesn't exist in this repo yet and is out of scope here
— don't attempt to add it.

### #5 — `src/providers/ring/auth/CredentialStore.cs`

Defense-in-depth only (the method is currently unreachable in production): on Windows, restrict the
ACL of the encrypted credential file (`authPath`, written by `Save()`) and the sanitized settings
file (`filePath`, rewritten by `SanitizeClearTextPassword`) to the current user, using the same
`DirectoryInfo/FileInfo.GetAccessControl/SetAccessControl` API already used in
`data.database.sqlite/DependencyInjection/ServiceCollectionExtensions.cs:40-61` — but inverted (that
existing code *grants* `BuiltinUsersSid` access; this needs to *restrict* to the current user via
`WindowsIdentity.GetCurrent().User`, disabling ACL inheritance). Gate with
`OperatingSystem.IsWindows()` and swallow failures the same way the existing precedent does
(sandboxed/restricted environments shouldn't crash the app over this).

### #6 — `src/providers/ring/video/VideoDownloader.cs`

Confirmed: `OpenStreamAsync` has zero production callers (leave it untouched — no value in
rewriting dead code and its stream-lifetime semantics are genuinely trickier to get right safely).
Rewrite `DownloadToFileAsync` to stream directly to disk instead of routing through the shared
`DownloadBytesAsync` byte-buffering helper:
```csharp
public async Task DownloadToFileAsync(string url, string saveAsPath)
{
    // validate url (same check as today)
    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    await using var fileStream = File.Create(saveAsPath);
    await response.Content.CopyToAsync(fileStream);
}
```
Every real caller (`RingMediaDownloadService.cs:369`, `RingVideoService.cs:944`,
`VideoDownloadClient.cs:67`) only inspects the file afterward (size, existence, hash) — confirmed
safe. `OpenStreamAsync` keeps using `DownloadBytesAsync` as-is.

### #7 — `src/data/database/data.database/Repositories/EventRepository.cs` + `IEventRepository.cs` + `EventEndpoints.cs`

1. Add `AsNoTracking()` to every read-only method on `EventRepository` (not the write methods) —
   `GetAsync`, `GetByProviderEventIdAsync`, `GetByApiSourceHashAsync`, `ListByDeviceAndDateRangeAsync`,
   `ListByLocationAndDateRangeAsync`, `ListByDeviceEventTypeAndDateRangeAsync`,
   `ListByLocationEventTypeAndDateRangeAsync`, `GetEventTypeSummaryAsync`,
   `ListUnansweredOrFlaggedAsync`, and `ListAsync`. This is a pure perf win with no behavior change
   (nothing currently mutates entities loaded through these paths) and directly benefits the two
   `BackupExportOrchestrator` full-table callers for free.
2. Add a genuine DB-level paginated method to `IEventRepository`/`EventRepository`:
   `Task<PaginatedResult<Event>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)`
   using real `OrderBy(...).Skip(...).Take(...)` (not the in-memory-after-full-load pattern the
   sibling Timeline/Integrity repos use) — reuse the existing `PaginatedResult<T>` type from
   `VideoForensics.Data.Common.Contracts` (`src/data/common/data.common/Contracts/PaginationModels.cs`).
3. Update `EventEndpoints.cs`'s `GET /api/v1/events` (the one real caller that needs bounding) to
   accept `pageNumber`/`pageSize` query params (sane default e.g. 100, hard cap e.g. 500) and call
   the new paginated method instead of `ListAsync`. Leave `BackupExportOrchestrator.cs`'s two calls
   to the plain `ListAsync` untouched — they legitimately need the full table for export/reconcile.

### #8 — `src/providers/ring/provider/Services/RingMediaDownloadService.cs` (1786 lines → split)

Extract 5 clusters the investigation confirmed are cleanly isolated (no/minimal field dependencies
beyond `_logger`/`_dataClient`), each following the file's own established pattern: an interface +
constructor-injected implementation with an optional constructor parameter that inline-`new`s a
default if not supplied (exactly how `IMediaMetadataTagger`/`FfmpegMediaMetadataTagger` already
work here) — **so no DI registration changes are required** in
`VideoForensicsHostingExtensions.cs:116-122`, and none of the three existing test files that
`new RingMediaDownloadService(logger, sessionProvider, dataClient)` directly need to change either.

| New file | Extracted from | Dependencies |
|---|---|---|
| `RingHistoryEventCache.cs` (+`IRingHistoryEventCache`) | `GetHistoryEventsAsync`, `IsHistoryCached`, history-cache fields (lines 71-74, 141-148, 1540-1585) | `ILogger` only |
| `RingDeviceHealthCapture.cs` (+`IRingDeviceHealthCapture`) | `CaptureDeviceHealthSnapshotAsync`, `GetDevicesForHealthAsync`, health-cache fields (99-102, 1468-1526) | `IVideoForensicsDataClient`, `ILogger` |
| `RingDeviceIdentityResolver.cs` (+`IRingDeviceIdentityResolver`) | `EnsureDeviceIdentityAsync`, `SetActiveProviderAccountId`, identity-cache fields (84-89, 1387-1460) | `IVideoForensicsDataClient`, `ILogger` |
| `RingMetadataSidecarWriter.cs` (+`IRingMetadataSidecarWriter`) | `WriteMetadataFile`, `WriteSnapshotMetadataFile`, `ValidateJsonSidecar`, `IsValidJpeg`, the private `Ring*Metadata` DTO records (1282-1385, 1608-1652, 1587-1606, 1738-1784) | `ILogger` only |
| `RingRetryPolicy.cs` (+`IRingRetryPolicy`) | `RetryWithBackoffAsync`, `IsRateLimitError`, retry constants (29-31, 1654-1690) | `ILogger` only |

Plus two **static** (no interface needed — pure functions, matching the existing `MediaFileNamer`/
`DeviceHealthMatcher` stateless-static pattern):
- `RingCvMetadataExtractor.cs` — `ExtractEventMetadata`, `ExtractMetadata` (910-1121). Note these two
  methods are near-duplicates of each other (one targets `Event`, one targets `MediaItem`); consolidate
  their shared logic into one parameterized private helper inside the new static class rather than
  copy-pasting both as-is — this is exactly the kind of duplication the class-smell finding called out.
- `RingProviderApiErrorClassifier.cs` — `ClassifyProviderApiError`, `GetHttpStatusCode`,
  `GetResponseBody` (1698-1736). **Must stay `internal` and keep (or move) the
  `InternalsVisibleTo("VideoForensics.Providers.Ring.Tests")` attribute** so
  `ProviderApiErrorClassificationTests.cs` can still reach it — update that test file's class
  reference from `RingMediaDownloadService.ClassifyProviderApiError` to the new class name.

Also delete `BuildNamespacedOutputPath` (confirmed dead code, no call sites).

**Leave in `RingMediaDownloadService` itself:** the two orchestration entry points
(`DownloadVideosAsync`, `DownloadSnapshotsAsync`), status/progress tracking (`GetStatus`,
`RecordBytesForRate`, `DrainActivityLog`, `InterlockedMax` — these are tightly entangled with the
per-event download loop and not worth forcing apart in this pass), `UpsertEventRecordAsync`
(calls into the new CV extractor + `SerializeMetadata`), and the small formatting statics
(`FormatBytes`, `EscapeMarkup`, `HumanizeExceptionTypeName`) — riding along since they're only used
by what remains. The orchestration methods change from inlining each cluster's logic to calling the
new injected collaborators (e.g. `_historyCache.GetEventsAsync(session, from, to, ct)` instead of
the inlined cache logic) — same behavior, just delegated.

## Execution

Per CLAUDE.md, delegate to Haiku subagents — **and, given a prior incident this session where a
subagent went out of scope and rewrote unrelated production code while chasing an unrelated build
error, every prompt below must explicitly instruct: touch ONLY the files listed; if you hit a
pre-existing, unrelated build/test failure, STOP and report it rather than fixing or working around
it; do not implement, complete, or modify any class not named in this task.**

Grouped by independence:
1. Finding #4 (`AesEncryption.cs`) — standalone.
2. Finding #5 (`CredentialStore.cs`) — standalone.
3. Finding #6 (`VideoDownloader.cs`) — standalone.
4. Finding #7 (`EventRepository.cs` + `IEventRepository.cs` + `EventEndpoints.cs`) — standalone.
5. Finding #8, part 1: the 2 static extractions (`RingCvMetadataExtractor`, `RingProviderApiErrorClassifier`)
   + updating `ProviderApiErrorClassificationTests.cs` — standalone, lowest risk.
6. Finding #8, part 2: the 4 stateful extractions (`RingHistoryEventCache`, `RingDeviceHealthCapture`,
   `RingDeviceIdentityResolver`, `RingRetryPolicy`) — standalone from each other and from part 1.
7. Finding #8, part 3: `RingMetadataSidecarWriter` extraction — standalone.
8. Finding #8, part 4: wire all 6 new collaborators into `RingMediaDownloadService`'s constructor
   (optional-param-with-fallback pattern) and replace the inlined logic in the two orchestration
   methods with calls to them — **must run after parts 1-3 land**, since it depends on their exact
   class/method names.

Tasks 1-7 can run in parallel (independent files). Task 8 runs after, sequentially.

## Verification

1. `dotnet build` each touched project as its subagent finishes; final `dotnet build` on the whole
   solution once task 8 completes.
2. `dotnet test` — the three existing `RingMediaDownloadService` test files
   (`RingMediaDownloadServiceTests.cs`, `RingIntegrationTests.cs`, `RingVideoProviderTests.cs`) and
   `ProviderApiErrorClassificationTests.cs` must still pass unmodified (except the one class-name
   reference update). `VideoDownloaderTests.cs` must still pass against the rewritten
   `DownloadToFileAsync`. Add a couple of new unit tests for the salt-file generation/reuse in
   `AesEncryption` and for the new `EventRepository.ListPaginatedAsync`.
3. Manually confirm `GET /api/v1/events?pageNumber=1&pageSize=50` returns a bounded page (start
   `VideoForensics.WebApp`, curl the endpoint).
4. Re-read the diff on `CredentialStore.cs`/`AesEncryption.cs` once more before considering done —
   these are security-sensitive files and deserve a second look given this session's earlier incident.
