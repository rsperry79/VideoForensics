# M7 - Offline sync engine

**Status on origin/dev (checked 2026-10-09):** STILL OPEN - no sync engine, local cache, or conflict policy exists.

**Source:** `plans/archive/maui-blazor-hybrid-conversion.md` line 23 (M7 "not started") and line 187 (M8 master/replica "mirrors the same designate-an-authority policy as M7", engine to be generic enough for client-to-server and replica-to-master). Supporting context: lines 165 (pull-only read cache), 384-385 (milestone definitions).

## Verdict

STILL OPEN. Greps on origin/dev for `offline`, `SyncEngine`, `OfflineSync`, `ConflictPolicy`, `MediaCache`, `LocalCache`, `ISyncEngine`, `/api/v1/sync`, `IChangeFeed`, `outbox`, `RowVersion` found nothing relevant in `src/client/**`, `src/core/api/**` or MAUI (only unrelated "Reconcile With Provider" in `MenuManager.cs`/`IEvidenceValidationService`). `git log origin/dev --grep=offline` shows only old MCP commits. MAUI is currently online-only: `MauiProgram.cs` registers `AddVideoForensicsClientApi()` (the `Remote*` classes) and caches only the server URL.

## Scope (what the archived plan got wrong or that has changed)

- Archived plan says "local SQLite cache". MAUI now has no data-layer reference (csproj references only Ui.Shared, Hosting, Core.Logging), and CLAUDE.md forbids MAUI referencing `data.*`. The cache must be a client-side store behind a new Client.Common contract using a standalone SQLite package (e.g. `sqlite-net-pcl`, per the prefer-NuGets rule), not `data.database.sqlite`/EF entities. The stale SQLitePCLRaw comment in the MauiApp csproj (lines 62-66) references a Sqlite project that is no longer referenced; clean up when touched.
- Per line 165 the client has no local write path, so this is pull-only refresh of a read cache, not two-way reconciliation. Conflict policy is trivially "server wins"; the real work is change detection, tombstones, and cache eviction. Keep the engine generic (source/target abstraction) so M8 replica-to-master can reuse it.
- M8's replica sync needs server-side change feeds; design them now as `/api/v1/sync/*` routes returning DTOs from `VideoForensics.Api.Contracts`.
- Media: server already has `IMediaStorageProvider` (`data.common/Contracts`), `MediaApiEndpoints.cs`, and ticket-based `RemoteMediaContentUrlProvider`. Media caching must work with tickets (short-lived URLs), so the cache downloads via authenticated stream and serves from a local file, not by persisting ticket URLs.
- Cached evidence is sensitive forensic data on a client device: encrypt at rest (SQLCipher or per-file encryption with a key from the platform credential store) and honor `AppLock`.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/core/api/VideoForensics.Api.Contracts/SyncDtos.cs` (NEW) | `SyncChangeDto`, `SyncPageDto`, cursor, tombstone DTOs | Wire contract for `/api/v1/sync/changes?since=` |
| `src/client/common/VideoForensics.Client.Common/Contracts/ISyncEngine.cs` (NEW) | `SyncAsync(ct)`, status/progress | Public API as interface |
| `src/client/common/VideoForensics.Client.Common/Contracts/ILocalCacheStore.cs` (NEW) | upsert/delete/read cursor | Abstract storage from engine |
| `src/client/common/VideoForensics.Client.Common/Contracts/IMediaCache.cs` (NEW) | get-or-fetch, evict, size cap | Media caching policy |
| `src/client/core/VideoForensics.Client.Core/Services/SyncEngine.cs` (NEW) | Pull loop, server-wins apply, tombstones | Platform-agnostic, unit-testable |
| `src/client/host/VideoForensics.Hosting/Remote/RemoteSyncSource.cs` (NEW) | HTTP client for change feed with bearer + `CancellationToken` | Follows `Remote*` pattern |
| `src/client/web/VideoForensics.WebApp/Api/SyncApiEndpoints.cs` (NEW; sibling of existing `MediaApiEndpoints.cs`) | `GET /api/v1/sync/changes` | Server change feed, uses ToDto mapping |
| `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs` | Register sync services (client vs server tier) | DI wiring |
| `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs` | Register cache store, trigger sync on `MauiServerConnectivityState` change | Wire into app |
| `src/client/maui/VideoForensics.MauiApp/Services/MauiServerConnectivityState.cs` | Raise reconnect event if not already exposed | Sync trigger |
| `src/client/maui/VideoForensics.MauiApp/VideoForensics.MauiApp.csproj` | Add SQLite NuGet; fix stale comment | Local store |
| Ui.Shared offline banner/status (path TBD) | Offline/last-synced indicator, localized via `IStringLocalizer<SharedResources>` + .resx | Required localization |
| New `.csproj` files (if any) | Must include `Microsoft.CodeAnalysis` PackageReference | Repo convention |

## Test plan

TDD, written first, expected to fail (compile/missing type, then behavior) before implementation.

- `SyncEngineTests` (new project/folder sibling to `src/client/VideoForensics.Client.Core.tests/`):
  - `SyncAsync_NewChanges_AppliesToCacheAndAdvancesCursor`
  - `SyncAsync_Tombstone_RemovesCachedItem`
  - `SyncAsync_ServerUnreachable_KeepsCacheAndReportsOffline`
  - `SyncAsync_Cancelled_DoesNotAdvanceCursor`
  - `SyncAsync_ConflictingLocalRow_ServerWins`
- `MediaCacheTests`: `GetAsync_Miss_FetchesAndStores`, `GetAsync_Hit_DoesNotCallServer`, `Evict_OverSizeCap_RemovesLeastRecentlyUsed`, `GetAsync_HashMismatch_DiscardsAndRefetches`.
- `RemoteSyncSourceTests` in `src/client/host/VideoForensics.Hosting.Tests` (mirror `RemoteEventRepositoryTests`): bearer attached, token forwarded, `/api/v1/sync/changes` route, DTO mapping.
- `SyncApiEndpointsTests` in `src/client/web/VideoForensics.WebApp.Tests` (mirror `MediaApiEndpointsTests`): cursor paging, unauthorized returns 401, tombstones included.
- `LocalCacheStoreTests`: persistence and encryption-at-rest round trip.
- Scoped runs via `dotnet test --filter` only.

## Risks and open questions

- Which entities are cached for offline review (events, media metadata, reports, cases)? Server rows need a monotonic change cursor and soft-delete/tombstones; today's schema may lack them (data-layer migration needed).
- Cache encryption and key custody on iOS/Android/Windows; evidence chain-of-custody implications of cached copies (integrity hash verification on cached media).
- Legal hold/deletion: cache must purge when the server deletes or revokes access (device unpair).
- Storage quota and eviction policy for media.
- MAUI desktop-first layout; mobile behavior deferred.
- M8 reuse: define engine against `ISyncSource`/`ISyncSink` now, but do not build replica features.
- Whether a server-side migration plus data-layer change is needed (CLAUDE.md schema-first rule) makes effort larger than the archived plan implies.

## Priority recommendation

**Low-Medium (recommend Low until requested).** Offline review is a convenience for a security-conscious forensics tool whose MAUI client already works online; encrypted client-side copies of evidence add security exposure; effort is large. **Effort: L.**

## Implementation dispatch

Split into sequential Sonnet batches, each test-first: (1) DTOs + server change feed + schema cursor/tombstones; (2) Client.Common contracts + `SyncEngine` + `RemoteSyncSource`; (3) cache store + encryption + `MediaCache`; (4) MAUI wiring + localized offline UI. Lite gate after each.
