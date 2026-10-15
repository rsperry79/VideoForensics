# M8 - Multi-server sync and secure network storage

**Status on origin/dev (checked 2026-10-09):** STILL OPEN. No `SecureNetworkMediaStorageProvider`, no master/replica or server-to-server sync code, and no M7 offline sync engine exist.

**Source:** `plans/archive/maui-blazor-hybrid-conversion.md` line 24 (M8 "not started, explicitly deferred"), lines 177-191 (section 4.2 secure network storage, section 4.3 master/replica), line 385 (M8 milestone scope).

## Verdict
Still open and deliberately deferred. The only prerequisite that exists is the M5 seam: `IMediaStorageProvider` with `LocalDiskMediaStorageProvider` as its sole implementation. M8 has two independent halves: (A) secure network storage, which is buildable now; (B) master/replica sync, which depends on M7 (also not built) and should not start before M7.

Grepped on origin/dev: `SecureNetworkMediaStorage`, `master.?replica`, `replica`, `multi-server`, `ServerSync`, `ISyncEngine`, `OfflineSync` (no relevant hits in src); `IMediaStorageProvider`, `LocalDiskMediaStorageProvider` (found); git log grep for sync/replica/network storage (only scheduled per-account provider event sync, #189, which is unrelated).

## Scope
Half A, secure network storage (the only near-term slice):
- Second `IMediaStorageProvider` implementation that writes under an OS-mounted share path (admin mounts SMB/NFS; the app does not speak the protocol).
- Mount-health check at startup and periodically, failing loudly if the path is not a live mount.
- Application-level per-file encryption wrapper, keyed via `IKeyStorageProvider` (core.forensics KeyManagement).
- Storage-location setting (via `IAppSettingRepository`) and a localized settings page.

Half B, master/replica (defer until M7 exists):
- Master-selection setting; replicas never call providers; replica authenticates to master via the existing pairing flow (`PairingEndpoints`, `PairedDeviceAuthHandler`); sync over `/api/v1`. Needs its own design doc after M7 lands.

Corrections and changes versus the archived plan:
- The download pipeline still writes directly with `File`/`FileStream` (`src/providers/ring/video/VideoDownloader.cs`, `snapshots/SnapshotManager.cs`, `core/Session.cs`). The only consumers of `IMediaStorageProvider` are `MediaApiEndpoints` and `MediaStillCaptureOrchestrator`. A network or encrypted provider does nothing for downloads until the write path is routed through the seam; the archive understated this prerequisite.
- `IMediaStorageProvider` lives in `src/data/common/data.common/Contracts/`; `LocalDiskMediaStorageProvider` in `src/client/host/VideoForensics.Hosting/`; DI registration is a singleton in `VideoForensicsHostingExtensions.cs` (~line 476).
- An encryption wrapper affects `MediaItem` SHA-256 integrity semantics (hash of plaintext) and range requests in `MediaApiEndpoints` (needs a seekable chunked format).
- Archive paths use the old `src/client` TUI layout; server code is now under `src/client/host` and `src/client/web`.

## Code and file touchpoints
| file | change | why |
|---|---|---|
| `src/data/common/data.common/Contracts/IMediaStorageProvider.cs` | Keep as is; add a separate health contract (new interface) rather than changing this one | avoid breaking existing mocks |
| `src/client/host/VideoForensics.Hosting/LocalDiskMediaStorageProvider.cs` | Reference only; stays default | baseline behavior |
| `src/client/host/VideoForensics.Hosting/SecureNetworkMediaStorageProvider.cs` NEW | Mounted-path provider with mount check and encryption wrapper | section 4.2 |
| `src/client/host/VideoForensics.Hosting/EncryptedMediaStream.cs` NEW | Seekable chunked AES-GCM stream | range requests, large video |
| `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs` | Select provider by storage-location setting (factory registration) | replaces fixed singleton |
| `src/core/helpers/core.helpers/core.forensics/KeyManagement/IKeyStorageProvider.cs` | Reuse for the media key; no change expected | keying per plan |
| `src/data/common/data.common/Contracts/IAppSettingRepository.cs` | Reuse for storage-location setting | persisted config |
| `src/client/web/VideoForensics.WebApp/Api/MediaApiEndpoints.cs` | Verify range/length handling on encrypted streams | streaming correctness |
| `src/providers/ring/video/VideoDownloader.cs`, `src/providers/ring/snapshots/SnapshotManager.cs`, `src/providers/ring/core/Session.cs` | Route writes through `IMediaStorageProvider` | otherwise downloads ignore network storage |
| `src/client/web/VideoForensics.WebApp/Api/NetworkSettingsEndpoints.cs` | Pattern reference (step-up on change) | consistency |
| `src/client/web/VideoForensics.WebApp/Api/StorageSettingsEndpoints.cs` NEW | `/api/v1` get/set storage location, DTO in Api.Contracts | client rules |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/NetworkSettings.razor` (+ `.resx`) | Pattern reference | localization rule |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/StorageSettings.razor` + `.resx` NEW | Location, mount status, plaintext-at-rest warning | sections 4.1/4.2 |

Half B files are intentionally not listed (depends on M7 design).

## Test plan
Write first; they fail initially because the types do not exist (compile failure, then assertion failures against stubs).
- Project `src/client/host/VideoForensics.Hosting.Tests`:
  - `SecureNetworkMediaStorageProviderTests`: `SaveAsync_ThenOpenRead_RoundTripsPlaintext`, `SaveAsync_WritesCiphertextOnDisk`, `ExistsAsync_MountMissing_Throws`, `OpenReadStreamAsync_TamperedFile_ThrowsAuthenticationFailure`, `DeleteAsync_ExistingFile_RemovesIt`.
  - `EncryptedMediaStreamTests`: `Seek_ToMidFile_ReadsCorrectPlaintext`, `Length_ReturnsPlaintextLength`.
  - `MediaStorageSelectionTests`: `AddHosting_DefaultSetting_ResolvesLocalDisk`, `AddHosting_NetworkSetting_ResolvesSecureNetwork`.
  - Moq `IKeyStorageProvider`.
- Project `src/client/web/VideoForensics.WebApp.Tests`: `StorageSettingsEndpointsTests` (`Set_NonSuperAdmin_Forbidden`, `Set_WithoutStepUp_Rejected`, `Set_InvalidPath_BadRequest`); extend `MediaApiEndpointsTests` with a range request over an encrypted stream.
- Ring downloader routing tests in the existing ring video tests project.
- Run scoped with `dotnet test --filter` only.

## Risks and open questions
- Key loss makes all network media unrecoverable; need key backup/export, and legal-hold implications for evidence.
- Plaintext vs ciphertext hash: chain-of-custody integrity records must define which hash is authoritative.
- Windows UNC paths and mapped drives differ by service account; a service running as LocalSystem cannot see user-mapped drives.
- Migration of existing media from local to network, and interplay with `BackupImportOrchestrator`.
- Half B: M7 is not built; conflict policy, replica promotion, and clock skew are undesigned.
- Open: is at-rest encryption wanted for the local default too (archive says no)?

## Priority recommendation
Low. Product value is niche (NAS users and multi-machine owners), security exposure is moderate (new key handling and a network write surface, mitigated by step-up auth), and effort is large. Effort: L overall (Half A: M; Half B: L and blocked on M7).

## Implementation dispatch
Half A only, three sequential Sonnet subagents, each tests-first: (1) `EncryptedMediaStream` plus tests; (2) `SecureNetworkMediaStorageProvider`, mount check, DI selection plus tests; (3) route Ring download writes through the seam plus storage settings endpoint/page/resx plus tests. Half B gets a design doc first.
