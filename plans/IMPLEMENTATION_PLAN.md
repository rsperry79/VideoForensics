# NuGet Dependency Replacement — Implementation Plan

**Date:** 2026-10-03  
**Status:** Approved for execution  
**Owner:** Engineering Team  
**Scope:** 4 phases, 15 discrete tasks, estimated 6-10 weeks

---

## Executive Summary

This plan breaks the NuGet audit findings into **4 phases of independent, testable tasks**, each following TDD-first (test → implementation → verify), CLAUDE.md standards (lite gate after each task, full gate before PR), and the branching model (all PRs to `dev`, then promotion to `main`).

**Phase 1 (Immediate, 1-2 weeks):** Unblock Windows key storage, consolidate caching, improve test data  
**Phase 2 (Short-term, 2-4 weeks):** Standardize retry logic, implement structured logging, audit crypto  
**Phase 3 (Medium-term, 4-8 weeks):** Date handling robustness, platform abstraction, network discovery  
**Phase 4 (Polish, as-needed):** CLI parsing, IP matching utilities

**Critical Path:** Phase 1 → Phase 2 (others can parallelize)

---

## PHASE 1: IMMEDIATE (1-2 Weeks)

### Task 1.1: Implement DPAPI Key Storage Provider
**Status:** 🔴 Critical | **Effort:** 1-1.5 days | **Risk:** Very Low

**Why:** Windows key storage currently non-functional (placeholder). Blocks forensic signing features on Windows.

**What:**
- Implement `DpapiKeyStorageProvider.cs` with System.Security.Cryptography DPAPI
- Generate RSA-2048 key pairs, store private key in DPAPI user data store
- Implement: `GenerateKeyPairAsync()`, `SignDataAsync()`, `VerifySignatureAsync()`, `DeleteKeyAsync()`, `ListKeysAsync()`
- Set `IsAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`

**Files:**
- `/src/core/helpers/core.helpers/core.forensics/KeyManagement/DpapiKeyStorageProvider.cs` (implement)
- `/src/core/helpers/core.helpers/core.forensics-tests/KeyManagement/DpapiKeyStorageProviderTests.cs` (update from "not implemented" to real behavior)

**Tests Changed:** 8 tests in `DpapiKeyStorageProviderTests.cs` go from assert-not-implemented to assert-crypto-operations

**TDD: Test First**
1. Update tests to expect real DPAPI behavior (key generation, signing, verification)
2. Implement using `System.Security.Cryptography` (DPAPI + RSA)
3. Run scoped tests: `dotnet test --filter "Class=DpapiKeyStorageProviderTests"`

**Potential Issues:**
- DPAPI is Windows-only; tests must conditionally skip or mock `RuntimeInformation.IsOSPlatform()`
- Ensure key file permissions are restrictive (NTFS ACLs)

**Branch:** `feature/dpapi-key-storage` off `dev`

---

### Task 1.2: Consolidate In-Memory Caching with IMemoryCache
**Status:** 🟡 High | **Effort:** 2-3 days | **Risk:** Very Low

**Why:** 5 cache implementations (Ring history, 2FA, WebAuthn, auth attempts, freshness) are duplicative. Consolidate into built-in `IMemoryCache`.

**What:**
- Migrate `RingHistoryEventCache` → `IMemoryCache` with widening logic preserved
- Migrate `TwoFactorPendingAuthCache`, `WebAuthnCeremonyCache`, `AuthAttemptCache` → `IMemoryCache`
- Keep `SemaphoreSlim` for lock protection (or use IMemoryCache lock pattern)
- Set appropriate TTLs via `MemoryCacheEntryOptions`

**Files:**
- `/src/providers/ring/provider/Services/RingHistoryEventCache.cs` (largest, complex)
- `/src/client/host/VideoForensics.Hosting/TwoFactorPendingAuthCache.cs`
- `/src/client/host/VideoForensics.Hosting/WebAuthnCeremonyCache.cs`
- `/src/client/web/VideoForensics.WebApp/Api/AuthAttemptCache.cs`
- All corresponding test files

**Tests Changed:** Test RingHistoryEventCache widening logic, TTL expiration, concurrent access under new IMemoryCache implementation

**TDD: Test First**
1. Write tests for IMemoryCache-based cache behavior (same scenarios, new implementation)
2. Implement replacement using `IMemoryCache.Set()` / `TryGetValue()`
3. Run scoped tests: `dotnet test --filter "Class~Cache"`

**Potential Issues:**
- **Widening logic:** Store both events and date range metadata together in cache key
- **Single-use semantics:** Use immediate key deletion on retrieval, or mock removal

**Branch:** `feature/imemory-cache-consolidation` off `dev`

---

### Task 1.3: Migrate Test Fixtures to Bogus
**Status:** 🟡 Medium | **Effort:** 1-1.5 days | **Risk:** None (test code only)

**Why:** Hand-written builders and fixtures are verbose. Bogus reduces boilerplate and improves test readability.

**What:**
- Replace `TestFixtures.cs` JSON mocks with Bogus `Faker` classes
- Replace `DoorbotHistoryEventBuilder.cs` → `Faker<DoorbotHistoryEvent>`
- Replace `SnapshotEventBuilder.cs` → `Faker<SnapshotEvent>`
- No test assertions change; only data generation mechanism

**Files:**
- `/src/providers/ring/core/tests/Mocks/TestFixtures.cs` (convert JSON to Faker)
- `/src/providers/ring/video/metadata/tests/Fixtures/DoorbotHistoryEventBuilder.cs` (convert builder to Faker)
- `/src/providers/ring/snapshots/metadata/tests/Fixtures/SnapshotEventBuilder.cs`
- All test files referencing builders

**Tests Changed:** All Ring video/snapshot tests that use builders; re-run with Bogus-generated data

**TDD: Test First**
1. Keep existing test cases; verify they still work with Bogus data
2. Implement Bogus Faker rules matching Ring API format quirks
3. Run scoped tests: `dotnet test --filter "Class~Ring.*Tests"`

**Potential Issues:**
- API response mocking (TestFixtures.cs) may need custom Bogus rules for inconsistent field names/types
- Seed Bogus with fixed seed for deterministic test output

**Branch:** `feature/bogus-test-fixtures` off `dev`

---

## PHASE 2: SHORT-TERM (2-4 Weeks)

### Task 2.1: Consolidate Retry Logic with Polly
**Status:** 🔴 Critical | **Effort:** 3-4 days | **Risk:** Low

**Why:** Custom retry logic in `HttpUtility.cs` and `RingRetryPolicy.cs` is duplicative and non-standard. Polly is the industry standard for resilience patterns.

**What:**
- Replace `HttpUtility.cs` custom ban/throttle logic with Polly `CircuitBreakerPolicy`
  - Hard ban (1 hour) → Circuit breaker open state
  - Base throttle (30s) with exponential backoff → Exponential backoff retry policy
  - Consecutive throttles escalation → Circuit-breaker threshold
- Replace `RingRetryPolicy.cs` with Polly `AsyncPolicy` (3 retries, 1s→30s exponential backoff)
- Integrate into `HttpClient` via `AddPolicyHandler()` in DI

**Files:**
- `/src/providers/ring/core/HttpUtility.cs` (extract ban/throttle → Polly policies)
- `/src/providers/ring/provider/Services/RingRetryPolicy.cs` (replace with Polly policy)
- Ring provider integration tests
- DI setup (if separate extension file exists)

**Tests Changed:** 
- `HttpUtilityTests.cs` — Test ban detection/persistence under Polly
- `RingRetryPolicyTests.cs` — Test max retries, backoff timing, rate-limit detection

**TDD: Test First**
1. Write tests for Polly policy behavior (circuit open after N 429s, exponential backoff, hard ban)
2. Implement Polly policies; wire into HttpClient
3. Run scoped tests: `dotnet test --filter "Class~(HttpUtility|RingRetry)"`

**Potential Issues:**
- Hard-ban persistence must integrate with Polly's circuit-breaker events (`OnBreak` handler)
- Per-request vs. global policy: Polly applies per HttpClient instance; ensure behavior matches current global ban

**Dependencies:** `Polly` NuGet (v8+)

**Branch:** `feature/polly-retry-consolidation` off `dev`

---

### Task 2.2: Implement Structured Logging with Serilog
**Status:** 🔴 Critical | **Effort:** 3-4 days | **Risk:** Low

**Why:** Custom audit logging is unstructured. Serilog provides structured events, better performance, and standard ecosystem integration.

**What:**
- Replace `ActionLogger.cs` file-based logging with Serilog `ILogger` + structured context
- Replace `FileLoggerProvider.cs` with `Serilog.Sinks.File` (JSON + text rolling files)
- Keep `IActionLogRepository` for dual-channel audit trail (file + database)
- Configure Serilog in `Program.cs` with enrichers for audit metadata (actor, entityType, entityId)

**Files:**
- `/src/core/core/core.logging/Services/ActionLogger.cs` (refactor to use Serilog)
- `/src/core/core/core.logging/Providers/FileLoggerProvider.cs` (replace with Serilog config)
- `/src/client/host/VideoForensics.Hosting/Program.cs` (add Serilog setup)
- Tests: `ActionLoggerTests.cs`, file output integration tests

**Tests Changed:**
- `ActionLoggerTests.cs` — Mock Serilog `ILogger`; verify LogAsync emits structured events
- New test: "Serilog.File outputs JSON-formatted audit logs with timestamp, actor, action"

**TDD: Test First**
1. Write tests for Serilog behavior (structured events, enrichment, file output)
2. Implement ActionLogger using Serilog ILogger + LogContext
3. Run scoped tests: `dotnet test --filter "Class~ActionLogger"`

**Potential Issues:**
- Dual logging (Serilog file + IActionLogRepository DB) must stay synchronized
- File I/O performance: Profile before/after; add async batching sink if needed

**Dependencies:** `Serilog`, `Serilog.Sinks.File`, `Serilog.Formatting.Json` (optional)

**Branch:** `feature/serilog-structured-logging` off `dev`

---

### Task 2.3: Security Audit Review — AES Encryption
**Status:** 🟡 Medium | **Effort:** 1-2 days (review) + design | **Risk:** Medium (security-sensitive)

**Why:** Home-rolled crypto always warrants formal audit. Decide: keep current AES-256 + PBKDF2, or migrate to NaCl?

**What:**
- Audit `AesEncryption.cs` against OWASP & NIST standards
- Verify: AES-256-CBC (good), PBKDF2 600k iterations (good for 2024), salt 16+ bytes, IV uniqueness
- Audit existing tests for completeness (round-trip, key derivation, IV uniqueness, timing attacks)
- **Decision:** Keep (with annual iteration update) or migrate to NaCl (simpler, audited)

**Files:**
- `/src/providers/ring/auth/Implementations/AesEncryption.cs` (review, no change yet)
- `/src/providers/ring/auth/tests/AesEncryptionTests.cs` (update/add tests)

**Tests Changed:**
- Verify existing tests cover round-trip, salt uniqueness, IV uniqueness
- Add test: "PBKDF2 iteration count meets OWASP minimum for current year"

**TDD: Test First**
1. Review/add tests for OWASP compliance (round-trip, salt, IV, iteration count)
2. Manually audit code for: salt source, PBKDF2 iterations, IV generation, timing attacks
3. Document decision: keep vs. NaCl

**Potential Issues:**
- Formal security audit may require external reviewer (out-of-scope for this plan, deferred)
- NaCl migration would require re-encrypting all persisted credentials (future work, post-audit)

**Branch:** `feature/aes-encryption-audit` off `dev` (or just PR if no code changes)

---

## PHASE 3: MEDIUM-TERM (4-8 Weeks)

### Task 3.1: Robust Date Parsing with NodaTime
**Priority:** 🟢 Low | **Effort:** 2-3 days

**What:** Migrate `DateTimeUtilities.cs` from `DateTime.TryParseExact()` to NodaTime for culturally-aware date parsing.

**Files:**
- `/src/client/core/VideoForensics.Client.Core/Utilities/DateTimeUtilities.cs`
- Tests: `DateTimeUtilitiesTests.cs`

**Branch:** `feature/nodatime-date-parsing` off `dev`

---

### Task 3.2: Platform Directory Resolution Abstraction
**Priority:** 🟢 Low | **Effort:** 1-2 days

**What:** Wrap `PlatformDirectoryService.cs` with `System.IO.Abstractions` for testability (no behavior change).

**Files:**
- `/src/core/helpers/core.helpers/Platform/PlatformDirectoryService.cs`
- Tests: `PlatformDirectoryServiceTests.cs`

**Branch:** `feature/system-io-abstractions` off `dev`

---

### Task 3.3: mDNS Service Advertisement Replacement
**Priority:** 🟢 Low | **Effort:** 2-3 days

**What:** Replace custom mDNS logic with Manatee.Mdns or Zeroconf NuGet.

**Files:**
- `/src/client/web/VideoForensics.WebApp/Discovery/MdnsAdvertisementService.cs`
- Tests: `MdnsAdvertisementServiceTests.cs`

**Branch:** `feature/manatee-mdns-discovery` off `dev`

---

## PHASE 4: POLISH (As-Needed)

### Task 4.1: CLI Argument Parsing with System.CommandLine
**Priority:** 🟢 Low | **Effort:** 1 day
**Scope:** Selftest CLI only

---

### Task 4.2: IP Address Matching with IPNetwork
**Priority:** 🟢 Low-Medium | **Effort:** 1 day
**Scope:** Ban IP matching in `BannedIpMatchService.cs`

---

## Execution Roadmap

### Recommended Task Order (for optimal parallelization)

**Week 1:**
- Start Task 1.1 (DPAPI) — highest priority, unblocks platform support
- Start Task 1.2 (IMemoryCache) in parallel — independent, high impact
- Start Task 1.3 (Bogus) in parallel — test code only, low risk

**Week 2:**
- Finish & PR Phase 1 tasks (all 3 merge to `dev`)
- Start Task 2.1 (Polly) — critical path, medium effort
- Start Task 2.2 (Serilog) in parallel — independent, critical path

**Week 3-4:**
- Finish & PR Phase 2 tasks (Polly + Serilog merge to `dev`)
- Task 2.3 (AES Audit) happens in parallel with Phase 2

**Week 5-6:**
- After Phase 2 complete, start Phase 3 tasks (NodaTime, System.IO.Abstractions, mDNS)

**Week 7-8:**
- Finish Phase 3; start Phase 4 if needed

---

## Testing Gates (Per CLAUDE.md)

### Lite Gate (after each task, before commit)
```powershell
# Build touched projects (incremental)
dotnet build src/core/helpers/core.helpers/core.forensics/core.helpers.csproj
dotnet build src/core/helpers/core.helpers/core.forensics-tests/core.helpers.tests.csproj

# Run scoped tests
dotnet test --filter "Class=DpapiKeyStorageProviderTests"
```

✅ **No warnings, no failures → commit**  
❌ **Failures → fix before commit**

### Full Gate (before opening PR)
```powershell
# Confirm with user: "Ready for full gate?"
# Update all NuGet packages
dotnet list VideoForensics.sln package --outdated  # Identify outdated packages
# Manually bump PackageReference Version to Latest in all .csproj files

# Clean rebuild
dotnet clean
dotnet build

# Run full test suite
dotnet test
```

✅ **All tests pass, no warnings → open PR to dev**  
❌ **Failures → fix before PR**

---

## Branching & Commits

**Per CLAUDE.md:**
- All work branches fork from `dev`
- Branch name: `feature/<task-name>` (e.g., `feature/dpapi-key-storage`)
- One commit per task (squash before PR)
- Commit message format:
  ```
  Phase 1: Task 1.1 — Implement DPAPI key storage provider

  Implement GenerateKeyPairAsync, SignDataAsync, VerifySignatureAsync, DeleteKeyAsync
  using System.Security.Cryptography DPAPI. Store private keys in DPAPI user data store.
  
  - Added DpapiKeyStorageProvider.cs with full RSA-2048 key management
  - Updated tests in DpapiKeyStorageProviderTests.cs (8 tests, all passing)
  - Platform guard: IsAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
  
  Lite gate: dotnet build + scoped tests passing
  
  Co-Authored-By: Claude Haiku 4.5 <noreply@anthropic.com>
  ```

**PR:** Each task is a separate PR into `dev` (not a mega-PR per phase)

**Promotion:** After Phase N tasks merge to `dev`, open `dev` → `main` promotion PR (squash-merge)

---

## Success Criteria

- ✅ Phase 1 complete: DPAPI works on Windows, caches consolidated, test data cleaner
- ✅ Phase 2 complete: Polly retry logic standardized, Serilog audit logging structured
- ✅ Full gate passes before any promotion to `main`
- ✅ No breaking changes to public interfaces
- ✅ All tests passing in CI
- ✅ Audit trail (ActionLogger → Serilog) functional in staging

---

## Risk Register

| Risk | Severity | Mitigation |
|------|----------|-----------|
| DPAPI platform-specific behavior | Medium | Tests conditionally skip on non-Windows CI; platform guard in code |
| IMemoryCache widening logic complexity | Medium | Keep existing tests; validate new implementation against old one |
| Polly circuit-breaker thread-safety | Low | Run stress tests with concurrent device downloads; Polly is battle-tested |
| Serilog file I/O bottleneck | Low | Profile before/after; add async batching sink if needed |
| AES migration (if adopted NaCl) | High | Defer NaCl adoption post-audit; keep existing AES until audit complete |
| Bogus randomization in tests | Low | Seed with fixed seed for deterministic output; document in test comments |

---

## Key Contacts & Escalation

- **Architecture questions:** Review CLAUDE.md "Project Structure" section
- **Test framework questions:** xUnit v3, Moq v4.20+, already in use
- **CI/CD questions:** GitHub Actions, `.github/workflows/` directory
- **NuGet package selection:** See NUGET_AUDIT.md for detailed analysis per package

---

**End of Implementation Plan**
