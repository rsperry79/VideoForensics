# Implementation plan: stored stills for the live-view video menu

Status: reviewed (Sonnet, read-only, 2026-11-28) and revised. Decisions made in chat. Ready to start Unit 1 on approval.

## Goal

Grab still on a live camera saves the frame as a still in the evidence store and keeps the PNG download. Copy still link then copies a link to that stored still. The link works only for a signed-in user who may see that camera.

## Decisions

- **Media kind: `Still`** (existing `MediaKinds.Still`, already used by review-page stills). Not `Snapshot`: snapshot queries, roll-up, repeat-marking and `GetLatestSnapshotHashAsync` would then pick up live grabs and could change how provider snapshots are skipped.
- **Access: Review role or higher** (existing `VideoForensicsPolicies.Review`). Unit 0 found no per-camera operator grant in the app, and the user chose role-based access. Camera-level limits are not in scope.
- **Audit: yes.** Each stored still writes an action-log entry with the operator and time. The row records the capturing operator.
- **Stored file is JPEG**, hashed server-side from the received bytes. The PNG download is unchanged and stays client-side.
- **Failed upload** shows an error and leaves Copy still link disabled. The PNG download still happens.
- **Only a live camera** can be grabbed, as now.

## Unit 0: done (findings, no code)

- Operators have roles only (ReadOnly, Review, Admin, SuperAdmin). No operator-to-camera grant exists. `CaseDevice` is case scope, not operator access. `RingLocationSharedUser*` is provider-side Ring sharing, not app access.
- Decision: role-based access, Review or higher.
- Link route: a new still viewer page. It asks for sign-in, then fetches a fresh content ticket. The copied link is the page, not the ticket URL (the ticket expires after 10 minutes).

## Unit 1: data write path (`src/data/common`, `src/data/database`)

Files:
- `src/data/common/src/Contracts/ILiveStillStore.cs` (new): `Task<Guid> StoreAsync(Guid deviceId, Guid operatorId, byte[] jpeg, CancellationToken ct)`.
- `src/data/common/src/Entities/MediaKinds.cs`: no change; use `Still`.
- `src/data/database/src/Repositories/...`: `LiveStillStore` implementation, injects `IMediaItemWriteRepository` directly (as the existing writers do), not through `UnitOfWork`.

Behaviour:
- SHA-256 of the received bytes, lowercase hex.
- Instant: server receive time, truncated to the millisecond. Store that, not the client's time.
- Path: resolve the media root as `SnapshotCycle` does (`ResolveMediaRootAsync`), then `MediaFolderLayout.GetDeviceFolder` and `MediaFileNamer.FormatMediaFileName(..., "still", "jpg")`. Store the path via `IMediaPathResolver.ToStoredAsync`. `MediaFormat = "jpg"`, `IsPlaceholder = false`, `MetadataJson = null`.
- Order: write the file first, then the row. If the insert fails, delete the file.
- Same device and instant, same bytes: return the existing id.
- Same device and instant, different bytes: reject with a conflict and log it. Do not silently replace.
- Concurrent insert: catch the unique-index violation on `(DeviceId, RecordedAtUtc, MediaKind)`, re-read, and apply the rules above.
- No migration expected (columns and index exist). Confirm during the first test run.

Tests first (`tdd`), in `src/data/database/tests/`:
- Row written with kind `Still`, format `jpg`, hash matching the stored bytes.
- Path matches the `MediaFolderLayout` convention.
- Same instant, same bytes returns one id.
- Same instant, different bytes is rejected.
- Insert failure deletes the file.
- `GetSnapshotsByDeviceAndDateRangeAsync` does not return a `Still` row (confirms the kind choice).

Done when: lite gate passes on touched projects; commit on the unit branch.

## Unit 2: upload endpoint and access (`src/client/web/VideoForensics.WebApp`)

Files:
- `src/client/web/VideoForensics.WebApp/src/Api/MediaApiEndpoints.cs`: new `POST /api/v1/media/live-stills/{deviceId}`. Follow the existing `/api/v1/media/{id}/stills` pattern: `MaintenanceEndpointFilter`, `VideoForensicsPolicies.Review`, media rate limit.
- Still read: the content route (`MediaApiEndpoints.cs:103`) with a fresh ticket issued to a signed-in Review-role operator. The viewer page calls it; the copied link is the viewer page.
- Access on upload and read: `VideoForensicsPolicies.Review`. No per-camera check.
- Kestrel and form body limits on the upload route; size cap 20 MB (same as the review still).
- Validation at the boundary: non-empty, size cap, JPEG magic bytes, and a decode check.

Tests (`src/client/web/VideoForensics.WebApp/tests/Api/`):
- Valid JPEG returns 200 with an id.
- PNG, truncated JPEG, empty body, oversized body are each rejected.
- Anonymous gets 401. Signed-in below Review gets 403. Review or higher gets 200.
- Expired ticket gets 401. A ticket for another item is rejected.
- Maintenance mode blocks the upload.
- Action-log entry is written with the operator.

Done when: lite gate passes; commit.

## Unit 3: client grab and link (`src/client/ui/VideoForensics.Ui.Shared`)

Files:
- `src/ts/liveview.ts`: `grabLiveFrame` returns the frame as a Blob (`canvas.toBlob`) and keeps the PNG download. Change the C# call at `LiveView.razor` from `InvokeVoidAsync` to `InvokeAsync`.
- New JS helper to POST the blob. Check first whether the upload goes through the browser (cookie or same-origin auth) or must go through the .NET HTTP client that holds the bearer token (remote host, MAUI). Pick the route that matches the auth in use; state which in the commit.
- `src/client/ui/VideoForensics.Ui.Shared/src/Pages/LiveView.razor`:
  - `GrabStillAsync`: download PNG, upload JPEG, then store the returned id in a separate `_lastStillId[id]`. `_stills[id]` stays the tile poster; do not reuse it as the link.
  - Copy still link enabled when `_lastStillId[id]` is set. Copy writes the app route URL with that id to the clipboard through a JS helper.
  - Upload failure sets `_error` (`GrabStillFailed`) and leaves the link disabled. Playback resumes if the grab paused it.
- `LiveView.resx`: new keys for copy success and upload failure. Localization rule applies (no literals in markup).
- Share `LoadStillAsync` with the new kind: it must query `Still` as well as `Snapshot`, so a grabbed still shows as the tile poster.

Tests (`src/client/ui/VideoForensics.Ui.Shared/tests/LiveViewPageTests.cs`), written first:
- Copy still link is disabled before any grab.
- Enabled after a successful upload; copies the app route with that id.
- Stays disabled after a failed upload; error shown.
- The PNG download still happens on a failed upload.
- Grab while paused resumes playback on failure.
- Copied link is the app route, not a ticket URL.

Done when: lite gate passes; commit.

## Unit 4: integration and verification

- Integration test through `MediaApiEndpoints`: grab, upload, row present as `Still`, file present, still route returns it to an authorised user.
- Purge test: confirm `RetentionService` handles `Still` the same as other media. If it does not, stop and report.
- Evidence feed: confirm the feed entry and the action-log entry appear.
- Visual check in a running app: still deferred; report whether it is needed.

Done when: full lite gate on touched projects passes; merge the unit branches into `wip`, pull, push `wip`.

## Risks

- **Evidence from a live frame.** A grab creates an evidence row. It cannot be removed from the menu; removal goes through purge.
- **Storage growth.** Each grab adds a file. No cap is set. Open question below.
- **Upload auth.** If the browser cannot reach the endpoint with its auth (remote host), Unit 3 needs a .NET relay. Found in Unit 3, not assumed here.
- **Hash.** Hash is computed server-side from received bytes; the client's hash is never trusted.

## Open questions (not blocking Unit 0)

- Storage cap per device per day, or no cap?
- Should a grabbed still get a case pin like review stills do?
