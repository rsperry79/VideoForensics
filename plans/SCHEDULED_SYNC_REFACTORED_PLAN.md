# Scheduled Background Sync Tasks (Refactored Approach)

**Decision:** REFACTORED (Better design, true concurrency)  
**Effort:** ~4 weeks  
**Strategic Value:** Operational reliability + multi-provider account support  
**Start:** After simple-mode + api-error-logging complete (or in parallel if team capacity)

---

## Why Refactored Approach

**Root Cause Problem:**
- `RingDeviceDiscoveryService` and `RingEventAndConfigService` call `SessionProvider.GetSession()` (parameterless)
- This is a "last account set" pointer — not account-aware
- Running two Ring accounts concurrently races on this shared pointer

**Sequential Mitigation (Original Plan):**
- Process accounts one-at-a-time within each provider type
- Trade-off: 1-2 min jitter between accounts
- Acceptable but suboptimal

**Refactored Approach (Chosen):**
- Fix the root cause: make all provider services account-aware
- `SessionProvider.GetSession(accountId)` instead of parameterless
- Enable true concurrent processing
- Better long-term design, no race conditions

**Cost-Benefit:**
- +2 weeks effort (~4 weeks total)
- Eliminates technical debt
- Enables future multi-account optimizations
- Sets pattern for other providers (Uniview, future ones)

---

## Phased Implementation

### Phase 0: Prepare (3-4 days)

**Goal:** Audit call sites, map refactor scope

1. **Find all `SessionProvider.GetSession()` calls (parameterless)**
   - Ring: RingDeviceDiscoveryService, RingEventAndConfigService, RingMediaDownloadService, RingAuthService, etc.
   - Uniview: Same pattern likely exists
   - Grep: `SessionProvider\.GetSession\(\)` — no args
   - Count: Estimate ~20-30 call sites

2. **Find all `SessionProvider` usages**
   - Which services hold `ISessionProvider` field?
   - Which ones are account-specific vs. global?
   - Identify the "last account set" pattern — where does it get set?

3. **Map the refactor:**
   - Change `SessionProvider.GetSession()` → `SessionProvider.GetSession(accountId)`
   - Add `accountId` parameter to service methods that call it
   - Trace upward: what methods feed `accountId` to these services?
   - Create call-site spreadsheet (file → method → call)

4. **Create tests for refactored signature**
   - Unit tests for `SessionProvider.GetSession(accountId)` before implementation
   - Mock tests for Ring services calling with explicit `accountId`

**Outcome:** Clear scope, test-first setup, no surprises in implementation

---

### Phase 1: Refactor SessionProvider (1 week)

**Goal:** Make SessionProvider account-aware

**Files:**
- `src/providers/ring/core/Session.cs` — Session class
- `src/providers/ring/core/SessionProvider.cs` — Provider class

**Changes:**

1. **Signature change:**
   ```csharp
   // Before:
   public Session GetSession() => _lastSession;
   
   // After:
   public Session GetSession(string accountId)
   {
       if (_sessions.TryGetValue(accountId, out var session))
           return session;
       throw new InvalidOperationException($"No session for account {accountId}");
   }
   
   // Keep for backward compat (deprecated):
   [Obsolete("Use GetSession(accountId) instead")]
   public Session GetSession() => _lastSession;
   ```

2. **Internal storage:**
   ```csharp
   private Dictionary<string, Session> _sessions = new();
   private string? _lastSessionAccountId;
   
   public void SetSession(string accountId, Session session)
   {
       _sessions[accountId] = session;
       _lastSessionAccountId = accountId;
   }
   ```

3. **Tests (test-first):**
   - GetSession(accountId) returns correct session
   - GetSession(unknown) throws
   - SetSession updates dictionary
   - Concurrent sets/gets don't race

**Review:** Carefully — this is critical path code

---

### Phase 2: Update Ring Services (1.5 weeks)

**Goal:** Pass accountId through all Ring service methods

**Files to update:**
- `RingDeviceDiscoveryService.cs` — GetDevicesAsync(...)
- `RingEventAndConfigService.cs` — GetEventsAsync(...), GetDeviceConfigAsync(...)
- `RingMediaDownloadService.cs` — DownloadEventsAsync(...), DownloadSnapshotsAsync(...)
- `RingAuthService.cs` — Authenticate(...)
- `RingRetryPolicy.cs` — If it uses SessionProvider
- Other Ring services as needed

**Pattern:**
```csharp
// Before:
public async Task<List<Device>> GetDevicesAsync(CancellationToken ct)
{
    var session = _sessionProvider.GetSession(); // ← race condition
    ...
}

// After:
public async Task<List<Device>> GetDevicesAsync(string accountId, CancellationToken ct)
{
    var session = _sessionProvider.GetSession(accountId); // ← explicit
    ...
}
```

**Tests:**
- Mock SessionProvider
- Pass various accountIds, verify correct session used
- No race condition tests (concurrent calls with different accountIds)

**Careful points:**
- Constructor injection patterns (what fields to update?)
- Backwards compat: can old code still call without accountId? (Maybe with default?)
- Interface changes: update IDeviceDiscoveryService, IEventAndConfigService, etc.

---

### Phase 3: Update Uniview Services (3-4 days)

**Goal:** Apply same pattern to Uniview providers

**Files:** Same structure as Ring
- `UniviewDeviceDiscoveryService.cs`
- `UniviewEventAndConfigService.cs`
- etc.

**Work:** Mirror Ring refactoring

---

### Phase 4: Update BackgroundServices (3-4 days)

**Goal:** Pass accountId to provider services from background sync loop

**Files:**
- `ProviderEventSyncService.cs` — Background service that loops over accounts
- `ProviderSnapshotSyncService.cs` — Similar

**Pattern:**
```csharp
// In the background service loop:
foreach (var account in accounts)
{
    foreach (var device in await _eventAndConfigService.GetEventsAsync(
        account.Id, // ← accountId now explicit
        fromTimestamp,
        ct))
    {
        // process event
    }
}
```

**No race condition:** Each iteration explicitly passes the accountId
- Two Ring accounts run concurrently
- Each calls GetSession(accountId) with their own ID
- No shared "last account set" pointer

---

### Phase 5: Tests & Integration (1 week)

**Goal:** Verify no regressions, concurrent operation works

**Tests:**
1. **Unit tests** per service (already written in Phases 1-4)
2. **Integration tests:**
   - Background sync service with 2+ Ring accounts
   - Concurrent sync of different accounts
   - Verify no cross-account contamination (account A doesn't see account B's devices)
3. **Regression tests:**
   - Single-account workflows still work (backwards compat)
   - MAUI/UI still works (unkeyed active provider resolution)
   - Existing background service (DeviceHealthSyncService) still works

**Lite gate:**
- `dotnet build` all providers + hosting
- `dotnet test --filter "...Ring.*\|...Uniview.*"` — provider tests
- `dotnet test --filter "...BackgroundService.*"` — background service tests

**Full gate (before PR):**
- Full `dotnet test` solution-wide

---

## Key Decisions Locked In

1. **Refactored, not sequential** — Fix root cause
2. **No service-factory abstraction** — Keyed DI (`AddKeyedScoped<IEventAndConfigService>("Ring", ...)`)
3. **Extend WebApp cross-platform** — Add `UseSystemd()` alongside `UseWindowsService()`
4. **Per-account schedule config** — DB table, not appsettings.json

---

## Dependencies

**Blocks:**
- Multi-provider automated background sync
- Linux service deployment with multiple Ring/Uniview accounts
- Future provider additions (Wyze, etc.)

**Blocked by:**
- None (independent)

---

## Execution Notes

- **Do NOT start this until:**
  - simple-mode + api-error-logging are complete or well-underway
  - Team has capacity for 4-week focused effort
  
- **Parallel work:**
  - Can happen alongside Phase C items (per-user-login, CI/CD) if team splits

- **Risk Mitigation:**
  - Test-first approach (write tests before implementation)
  - Regression test suite covers existing workflows
  - Careful code review on SessionProvider changes (critical path)

- **Communication:**
  - Commit per phase (SessionProvider → Ring → Uniview → BackgroundServices → Tests)
  - Each commit testable + buildable

---

## Success Criteria

✅ SessionProvider.GetSession(accountId) working, backward compat in place  
✅ Ring services account-aware (all call sites updated)  
✅ Uniview services account-aware  
✅ Background sync services use explicit accountId  
✅ No concurrent account race conditions  
✅ Backwards compat: single-account workflows work  
✅ Lite gate passes  
✅ Full test suite passes  
✅ PR merged to dev  

---

## Timeline Estimate

| Phase | Duration | Notes |
|-------|----------|-------|
| Phase 0: Audit & Prepare | 3-4 days | Careful scope mapping |
| Phase 1: SessionProvider | 1 week | Critical path, careful review |
| Phase 2: Ring Services | 1.5 weeks | Most call sites, most risky |
| Phase 3: Uniview Services | 3-4 days | Mirror Ring pattern |
| Phase 4: BackgroundServices | 3-4 days | Wiring up background sync |
| Phase 5: Tests & Gate | 1 week | Regression suite, full gate |
| **Total** | **~4 weeks** | Start after Phase B complete |

