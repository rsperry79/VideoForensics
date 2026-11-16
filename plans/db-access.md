# Plan: one gated database write path in VideoForensics.Data

Status: revised v3 after the Opus review and the Sonnet review (agent ad33abd3f2f396bb9). Not started.

## Root cause (verified)
- `UnitOfWork.ExecuteAsync` serializes writes through `ActionLogAppendGate` (process-wide `SemaphoreSlim(1,1)`, `src/data/database/src/Repositories/ActionLogAppendGate.cs`).
- Most standalone repositories (about 66 files inject `IDbContextFactory`) open their own context, call `SaveChanges` immediately, and do not take the gate. `ActionLogRepository` and `MediaFlagRepository` do take it.
- Gated transactions and ungated writes contend at SQLite. The ungated side waits `Default Timeout=5` (`src/data/database/sqlite/src/DependencyInjection/SqliteConnectionStrings.cs:19`), then fails with `database is locked`.
- Ring `ReplaceAsync` writers (`RingEventHistoryWriter`, `RingAccountSettingsWriter`) open their own transactions outside `UnitOfWork`, so they are ungated.

## Goal
Every database write in production code goes through one re-entrant gate. Reads stay ungated (WAL allows concurrent reads).

## Location
- `IUnitOfWork` is in `src/data/common/src/Contracts/IUnitOfWork.cs` (namespace `Data.Common.Contracts`). It is not in `src/data/core`. The gate work lands there and in `src/data/database`.

## Decisions (recorded)
1. Repository writes: **context-injected refactor.** Repositories take the ambient unit-of-work context and drop `IDbContextFactory`.
2. `BackupImportOrchestrator` (`Client.Core`): **server endpoint.** The client calls a versioned `/api/v1` endpoint; the import runs on the server through the gate.
3. `Default Timeout`: **keep 5 seconds** until the gate lands.

## Decisions (recorded, block U3)
A. No ambient unit of work: a standalone repository write **throws**. Every write runs inside a unit of work.
B. The ~18 `UnitOfWork*Repository` clones inside `UnitOfWorkContext` are **deleted**. Standalone repositories become the single implementation and take the ambient context.
C. `VACUUM` and `wal_checkpoint(TRUNCATE)` use a **transaction-less gate entry**.

## Design
1. **Gate mechanism.** Rename `ActionLogAppendGate` to a general write gate. The gate owns the re-entrancy flag (`IsHeldByCurrentFlow`). `AmbientUnitOfWork` keeps holding the `DbContext`.
2. **Nested calls.** A nested `ExecuteAsync` or `ReadAsync` inside a write reuses the ambient context. It never opens a second connection while the gate is held. Today a nested call skips the wait but still opens a new connection, so it self-blocks for 5 seconds. This is a fix to existing behaviour.
3. **Reads.** `ReadAsync` stays ungated outside a write. Inside a write it sees uncommitted changes. This is a behaviour change and is called out in U1.
4. **Caveat.** The caller that enters the write sets the flag. An async helper's write is not visible to its caller.
5. **Context-injected repositories.** Standalone repositories take the ambient context and drop their factory (decision 1). Writes run under the gate. Standalone repositories currently save immediately; the ambient context saves at commit, so callers that expect immediate visibility must be checked in U3.
6. **Other writes through the gate:** raw SQL, `ExecuteDeleteAsync`, `SqliteEventTypeLinkBackfill` (has its own transaction), `ReferenceDataSeeder`, `DingIdRepairService`, `EventBackfillService`, `SecurityAuditService`, `SnapshotRollupService`.
7. **Maintenance.** `VACUUM` and `wal_checkpoint(TRUNCATE)` use the transaction-less gate entry (decision C). Scheduled when no write is queued (`SqliteDatabaseEngine`, `DatabaseMaintenance`, `SqliteDatabaseMaintenanceService`).
8. **Short transactions.** Batch retention purge, backfill and Ring replace. Keep file I/O and credential crypto out of the gate.
9. **Timeouts.** Queue wait is logged per write. A waiter that exceeds a queue timeout fails with a clear error. The queue timeout must be set below `Default Timeout` so the queue error comes first. Pick the values in U2.
10. **Client boundary.** `BackupImportOrchestrator` moves behind a server endpoint. Client hosts keep no reference to `data.*`. The endpoint needs auth, versioning and request-size decisions, which are not yet written down.
11. **Out of process.** dbrepair, diagnostics and a second host instance are not covered by an in-process gate. They keep `Default Timeout` as the backstop.

## Enforcement
- An allowlist of files that may still inject `IDbContextFactory` is committed in U1. Each unit removes entries. U6 becomes "the allowlist is empty except UnitOfWork, the gate, and the dbrepair, dbsetup, diagnostics and test fixtures."
- Tests and utility tools keep the factory and are on the allowlist.

## Units (each: red test first, record the red run)
- [ ] U1. Enforcement and gate tests. Allowlist test. Writes never overlap. Nested write-in-write reuses the context and does not deadlock. Read-in-write sees uncommitted changes. Parallel-in-write is detected or forbidden. A repo write outside the gate fails the allowlist test.
- [ ] U2. Extend the gate: re-entrancy, queue-wait telemetry, queue timeout (value below `Default Timeout`), transaction-less entry.
- [ ] U3. Move standalone repository writes to context injection, one commit per repository group. Requires open decisions A and B. Tests green after each.
- [ ] U4. Move non-repository writes (maintenance, backfills, seeders, Ring writers) through the gate. Batch the retention, backfill and Ring replace transactions.
- [ ] U5. Replace `BackupImportOrchestrator` direct access with a server endpoint. Independent of U2–U4; can run in parallel after U1. Needs the auth, versioning and request-size decisions.
- [ ] U6. Shrink the allowlist to the permitted set (see Enforcement).
- [ ] U7. Verify live: no `database is locked` under snapshot and backfill load, with queue-wait metrics attached.

## Before migrating anything
- Confirm from `Slow unit of work` log entries which writers hold the lock during failures (`src/data/database/src/Repositories/UnitOfWork.cs`, `LogTransaction`).

## Risks
- A serialized writer makes each write wait for the longest one ahead of it. Keep transactions short (design 8).
- Immediate-save callers break if the ambient context saves at commit (design 5).
- Diff is large (about 675 references across 156 non-test files). Each unit is committed and tested separately.

## Worktree
Plan doc: branch `plans/db-access` in the docs repo (commit `aa028f7`). Implementation: branch `feature/db-access` at `F:\vf-worktrees\db-access`.
