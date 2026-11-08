# Plan: one gated database access path in VideoForensics.Data.Core

Status: saved, not started. Revised after the Opus review (agent a3b7b152458ff6408).

## Root cause
- `UnitOfWork.ExecuteAsync` serializes writes through `ActionLogAppendGate` (process-wide `SemaphoreSlim(1,1)`).
- About 70 standalone repositories open their own context through `IDbContextFactory` and call `SaveChanges` without the gate.
- Gated transactions and ungated writes contend at the SQLite level. The ungated side waits `Default Timeout=5` (`src/data/database/sqlite/src/DependencyInjection/SqliteConnectionStrings.cs`), then fails with `database is locked`.
- Ring `ReplaceAsync` writers (`RingEventHistoryWriter`, `RingAccountSettingsWriter`) open their own transactions outside `UnitOfWork`, so they are also ungated.

## Goal
Every database write in production code goes through one re-entrant gate. Reads stay ungated (WAL allows concurrent reads).

## Decisions (recorded)
1. Repository writes: **context-injected refactor.** Repositories take the ambient unit-of-work context and drop `IDbContextFactory`.
2. `BackupImportOrchestrator` (`Client.Core`): **server endpoint.** The client calls a versioned `/api/v1` endpoint; the import runs on the server through the gate.
3. `Default Timeout`: **keep 5 seconds** until the gate lands.

## Design
1. **Extend `IUnitOfWork`** (`src/data/core/src/Contracts`).
   - `ReadAsync` and `ExecuteAsync` stay the public entry points, with XML docs.
   - Re-entrancy: a nested `ExecuteAsync` or `ReadAsync` inside a write reuses the ambient write context (`AsyncLocal` held-flag, replacing `AmbientUnitOfWork`). It never opens a second connection while the gate is held.
   - Caveat, documented: the caller that enters the write sets the flag. An async helper's write is not visible to its caller.
2. **Context-injected repositories.** Each of the ~70 standalone repositories takes the ambient context from the unit of work and drops its factory. Writes run under the gate.
3. **Other writes through the gate:** raw SQL, `ExecuteDeleteAsync`, `SqliteEventTypeLinkBackfill` (has its own transaction), `ReferenceDataSeeder`, `DingIdRepairService`, `EventBackfillService`, `SecurityAuditService`, `SnapshotRollupService`.
4. **Maintenance.** `VACUUM` and `wal_checkpoint(TRUNCATE)` need exclusive access. Run them through the gate, scheduled when no write is queued (`SqliteDatabaseEngine`, `DatabaseMaintenance`, `SqliteDatabaseMaintenanceService`).
5. **Short transactions.** Break retention purge, backfill and Ring replace into batches. Keep file I/O and credential crypto out of the gate.
6. **Telemetry and timeouts.** Log queue wait per write. A waiter that exceeds a set timeout fails with a clear error, not an unbounded wait.
7. **Client boundary.** `BackupImportOrchestrator` moves behind a server endpoint (decision 2). Client hosts keep no reference to `data.*`.
8. **Out of process.** dbrepair, diagnostics and a second host instance are not covered by an in-process gate. They keep `Default Timeout` as the backstop.

## Units (each: red test first, record the red run)
- [ ] U1. Gate tests: writes never overlap; nested write-in-write reuses the context and does not deadlock; read-in-write sees uncommitted changes; parallel-in-write is detected or forbidden; a write that bypasses the gate fails a repo-level test.
- [ ] U2. Extend `IUnitOfWork`: re-entrancy, queue-wait telemetry, queue timeout.
- [ ] U3. Move standalone repository writes to context injection, one commit per repository group. Tests green after each.
- [ ] U4. Move non-repository writes (maintenance, backfills, seeders, Ring writers) through the gate.
- [ ] U5. Replace `BackupImportOrchestrator` direct access with a server endpoint.
- [ ] U6. Remove `IDbContextFactory` from production callers. Ask before any compatibility shim.
- [ ] U7. Verify live: no `database is locked` under snapshot and backfill load, with queue-wait metrics attached.

## Before migrating anything
- Confirm from `Slow unit of work` log entries which writers hold the lock during failures (`src/data/database/src/Repositories/UnitOfWork.cs`, `LogTransaction`).

## Risks
- A serialized writer makes each write wait for the longest one ahead of it. Keep transactions short (design 5).
- Nested reads or writes that open a second connection block on the outer gate and fail after `Default Timeout`. The re-entrancy flag prevents this.
- The diff is large (about 675 references across 156 non-test files). Each unit is committed and tested separately.

## Worktree
Plan doc: branch `docs/db-access-plan` at `F:\vf-worktrees\db-access-plan`. Implementation will use its own feature branch and worktree.
