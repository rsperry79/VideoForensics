# Plan: stored stills for the live-view video menu

Status: drafted, awaiting approval in chat. Not started.

## Goal

"Grab still" on a live camera saves the frame as an evidence snapshot. "Copy still link" then works for that frame. Both user choices are fixed:
- Stored as a snapshot media item (listed in Media and the evidence feed, hashed).
- The link opens only for a signed-in user who can already see that camera.

This reverses the earlier download-only choice. A grabbed frame now creates an evidence record.

## Decisions carried into the build

- Grab still keeps its PNG download, and also uploads the same frame to the server. The upload is the stored still. Download stays for the user who grabbed it.
- The stored file is a JPEG, not a PNG, to match provider snapshots. The download stays PNG.
- A frame that fails to upload is reported as failed. The menu does not claim a stored still that does not exist.
- Only a camera that is live can be grabbed (as now).

## Unit 1: data model and write path

- Use the existing `MediaItem` row. `MediaKind` gets the snapshot kind used by provider snapshots, so `GetSnapshotsByDeviceAndDateRangeAsync` returns it and LiveView's `LoadStillAsync` picks it up with no change.
- Add `IMediaItemWriteRepository` use through `UnitOfWork` for the insert. No new table, so no migration is expected. Confirm in the first test run.
- New service `LiveStillStore` (contract in `src/data/common/src/Contracts/`):
  - Input: device id, JPEG bytes, captured-at UTC.
  - Computes SHA-256 (lowercase hex, to match `GetLatestSnapshotHashAsync`).
  - Writes the file under the device's existing evidence folder, resolved through `StorageLocationProvider`. The folder convention is read from the existing snapshot download code, not guessed.
  - Inserts the row. `IsPlaceholder` is false.
  - Returns the new media item id.
- Unique key (device, recorded instant, kind): a second grab in the same instant returns the existing row instead of failing.
- Tests first (`tdd`): insert, hash match, duplicate instant returns the same row, file written under the device folder.

## Unit 2: upload endpoint and access

- `POST /api/media/live-stills/{deviceId}`: accepts `image/jpeg`, validated at the boundary (size cap, JPEG magic bytes, non-empty). Returns the media item id.
- `GET /api/media/{id}` serves the still with the signed-in user's camera-access check. Signed-out requests get 401. Signed-in users without access get 403. Access is checked against the same device-access rule the live view uses.
- Tests for each case: upload ok, bad type, too large, no login, no access, with access.

## Unit 3: client grab and link

- `liveview.ts`: `grabLiveFrame` returns the JPEG blob (canvas `toBlob`) and posts it through a new JS helper. The PNG download stays.
- `LiveView.razor`:
  - `GrabStillAsync` awaits the upload, then sets `_stills[id]` to the new media URL.
  - Copy still link is enabled when `_stills[id]` has a URL. The copy writes that URL to the clipboard (`navigator.clipboard`, through a JS helper).
  - Failure sets `_error` with `GrabStillFailed` (existing key) and leaves the link disabled.
- New resource keys in `LiveView.resx`: copy success and upload failure messages. Localization rule applies.
- Tests: the link is disabled before a grab, enabled after a successful upload, stays disabled after a failed upload, and the download still happens.

## Unit 4: review and verification

- Lite gate on touched projects (`dotnet build`, `dotnet test --filter`).
- Full check of the Media list: a grabbed still appears as a snapshot, and its evidence feed entry is correct.
- Visual check in a running app is still not done. Stop and report if it is needed.

## Risks

- Evidence record from a live frame: this is the reversal named above. A grab creates a record that cannot be purged from the menu.
- Storage growth: each grab adds a file. No cap is planned. Confirm this is acceptable before unit 1 starts.
- Hash of a frame re-encoded to JPEG differs from the browser frame. The stored hash is of the stored file, which is the evidence.

## Open items for approval

- Whether the purge flow should treat a live still like any other snapshot (planned: yes).
