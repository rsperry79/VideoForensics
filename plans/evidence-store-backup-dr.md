# Evidence store backup and disaster recovery

**Status on origin/dev (checked 2026-10-09):** CHANGED. A manual metadata-only export/import exists; scheduled backups, media-file backup, retention, off-box copy and restore verification do not.

**Source:** `plans/archive/maui-blazor-hybrid-conversion.md` line 458: "Backup/disaster-recovery of the centralized evidence store was considered and explicitly deferred ... worth revisiting given the server becomes the sole copy of the evidence."

## Verdict

CHANGED, partly done. Commit `02fc4f6` ("add event reimport support via DB backup export/import") added `IBackupExportService` / `IBackupImportService`, `BackupEndpoints` (`/api/v1/backup/{prepare-export,export,import}`, SuperAdminLocal + step-up + audit) and the `ImportExport.razor` page. That is a manual, operator-triggered, JSON-only export (accounts, locations, devices, events, download events, media_items metadata) and does NOT include the media files themselves or the other entity tables (operators, security audit log, legal holds, device pairings). There is no scheduled backup, no retention, no off-box target, and no restore drill, so the "server is the sole copy of the evidence" risk is still open.

## Scope

Archived plan was written before the manual export existed; update its premise. Verified existing pieces on origin/dev:
- `DatabaseInitializer.BackupDatabaseIfExistsAsync` makes a pre-migration file copy of the DB (`.bak-<timestamp>`), only when migrations are pending, and uses a raw `File.Copy` (not WAL-safe; `VACUUM INTO` / SQLite online backup API is better).
- `StorageCategory.Backup` -> `StorageSettingsService` (maps to `QueryExportLocation`) already gives a configurable backup directory concept.
- `IMediaStorageProvider` / `LocalDiskMediaStorageProvider` is the media seam; `ScheduledSyncService` + `ScheduledSyncRegistrationExtensions` is the pattern for a hosted background job.
- `EvidenceRetentionPolicy` and legal hold exist: backup retention must never prune the only copy of held evidence and backups must not defeat legal-hold deletion semantics (document the interaction).

Proposed scope (phased):
1. Scheduled, WAL-safe database snapshot (`VACUUM INTO`) with SHA-256 manifest, configurable schedule and keep-N retention, written under `StorageCategory.Backup`.
2. Media backup: incremental copy of media files via `IMediaStorageProvider` (hash-verified, append-only) to a configured secondary path (external disk / mounted network share); no app-implemented SMB.
3. Restore: documented procedure plus a `restore --verify` dry-run that checks DB integrity (`PRAGMA integrity_check`) and re-hashes media against `media_items` hashes.
4. Audit: new `SecurityAuditEventTypes` entries (BackupScheduledRun, BackupFailed, RestoreVerified); urgent notification on repeated failure (reuse existing urgent-notification path).
5. Out of scope: cloud targets, multi-server sync (M8), encrypting media.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/client/common/VideoForensics.Client.Common/Contracts/IBackupExportService.cs` | Leave; add sibling contracts | Existing JSON export stays for portable re-import |
| `src/client/common/VideoForensics.Client.Common/Contracts/IEvidenceBackupService.cs` | NEW: snapshot DB + sync media + prune | Public API as interface in Contracts/ |
| `src/client/common/VideoForensics.Client.Common/Contracts/IBackupVerificationService.cs` | NEW: integrity check + media hash re-verify | Restore drill |
| `src/client/core/VideoForensics.Client.Core/Services/EvidenceBackupService.cs` | NEW implementation | Business logic beside `BackupExportOrchestrator.cs` |
| `src/client/host/VideoForensics.Hosting/BackgroundServices/ScheduledBackupService.cs` | NEW hosted service modeled on `ScheduledSyncService.cs` | Scheduling |
| `src/client/host/VideoForensics.Hosting/ScheduledSyncRegistrationExtensions.cs` | Add registration (or sibling extension) | DI wiring |
| `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs` | Register new services near line ~406 | DI |
| `src/data/database/sqlite/data.database.sqlite/Migrations/DatabaseInitializer.cs` | Replace `File.Copy` with WAL-safe snapshot helper | Correctness of existing backup |
| `src/core/api/VideoForensics.Api.Contracts/BackupDtos.cs` | Add status/schedule DTOs | Wire contracts for UI |
| `src/client/web/VideoForensics.WebApp/Api/BackupEndpoints.cs` | Add GET `/api/v1/backup/status`, POST `/api/v1/backup/run`, POST `/api/v1/backup/verify` (SuperAdminLocal, step-up on run/verify, audit) | Control surface |
| `src/client/host/VideoForensics.Hosting/Remote/RemoteBackupExportService.cs` (+ new `RemoteEvidenceBackupService.cs` NEW) | Remote* client with CancellationToken, bearer auth | Client/server split |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/ImportExport.razor` | Status panel (last run, last verified, failures); all strings via `L[...]` + `.resx` | Visibility, localization rule |
| `src/data/common/data.common/Entities/SecurityAuditLogEntry.cs` / `SecurityAuditEventTypes` | New event type constants | Audit trail |

## Test plan

TDD, tests first; each should fail first with compile/NotImplemented or assertion failure.
- `src/client/VideoForensics.Client.Core.tests/EvidenceBackupServiceTests.cs`: `RunAsync_WithWalDatabase_ProducesConsistentSnapshot`, `RunAsync_MediaAlreadyCopied_SkipsUnchangedFiles`, `RunAsync_HashMismatchAfterCopy_FailsAndReportsError`, `Prune_KeepsNewestN_DeletesOlder`, `RunAsync_DestinationUnavailable_ReturnsErrorWithoutDeletingExisting`, `RunAsync_Cancelled_StopsAndLeavesNoPartialSnapshot`.
- `BackupVerificationServiceTests.cs` (same project): `Verify_CorruptDb_ReportsIntegrityFailure`, `Verify_MediaHashChanged_ReportsMismatch`.
- `src/client/host/VideoForensics.Hosting.Tests/ScheduledBackupServiceTests.cs`: `Tick_DueSchedule_RunsBackup`, `Tick_RepeatedFailures_RaisesUrgentNotification`; `RemoteEvidenceBackupServiceTests.cs`: forwards token, uses `/api/v1/backup/...`.
- `src/client/web/VideoForensics.WebApp.Tests/BackupEndpointsTests.cs`: `Run_WithoutSuperAdminLocal_Returns403`, `Run_WithoutStepUp_Rejected`, `Run_Success_WritesAuditEntry`.
- `data.database.sqlite` tests: `BackupDatabaseIfExists_WalMode_SnapshotContainsRecentWrites`.
- Ui.Shared test: status panel text resolved via localizer keys.
- Scoped runs only, via `dotnet test --filter`.

## Risks and open questions

- Backups are a copy of the whole evidence set; the destination must be access-controlled and (for DB) ideally encrypted. If SQLCipher at-rest encryption is active, the key must be backed up separately or the backup is unrecoverable; decide key-escrow story first (not verified on dev whether SQLCipher is shipped).
- Legal hold / retention: deletion of held-or-expired evidence vs. backup copies persisting; need a policy statement.
- Storage size: media is large; incremental-only and free-space precheck required.
- Existing JSON export is not a true DR path (no media bytes, partial tables); decide whether to extend it or keep it as a "portable re-import" tool only.
- Restore into a running server is risky; prefer offline restore CLI/procedure.
- Chain of custody: restored evidence must keep original hashes; verify and log.
- Open: single secondary path vs. multiple targets; backup frequency default.

## Priority recommendation

High: the server is the sole evidence copy and a disk failure would be unrecoverable, the existing manual export gives false comfort (no media), and exposure is moderate (backups widen the data footprint, so it needs SuperAdminLocal/step-up/audit). Effort: L (phase 1 alone is M).

## Implementation dispatch

Sonnet subagents, each test-first: (A) WAL-safe snapshot helper + `DatabaseInitializer` fix; (B) `EvidenceBackupService` + verification service + contracts; (C) hosted scheduler + DI + audit events; (D) endpoints + DTOs + `Remote*` client; (E) UI status panel + `.resx`. Lite gate after each; ask before the full gate.
