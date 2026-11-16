# NuGet Dependency Audit — VideoForensics

**Date:** 2026-10-03  
**Scope:** Full repository scan for handrolled code replaceable with maintained NuGet packages  
**Total Custom Classes Found:** ~120+  
**Immediate Replacement Candidates:** ~40

---

## 🔴 SECURITY-SENSITIVE CODE (Audit Required)

### AES Encryption — `src/providers/ring/auth/Implementations/AesEncryption.cs`
- **Current:** Cross-platform AES-256-CBC + PBKDF2 (600k iterations), custom salt handling
- **Risk Level:** Medium (home-rolled crypto always warrants review)
- **Recommendation:** 
  - ✅ Implementation appears secure (PBKDF2 iterations are appropriate)
  - **Action:** Audit against OWASP key derivation standards; consider **NaCl** or **libsodium-net** as drop-in replacement (simpler API, audited by cryptographers)
  - **Effort:** Medium—affects credential encryption on non-Windows platforms

### Key Management Providers — `src/core/helpers/core.helpers/core.forensics/KeyManagement/`
- **Files:** `DpapiKeyStorageProvider.cs`, `FileBasedKeyStorageProvider.cs`, `TpmKeyStorageProvider.cs`
- **Status:** 
  - DPAPI provider is a non-functional placeholder
  - TPM provider may have platform gaps
  - File-based provider is a security boundary (ensure file perms are locked down)
- **Recommendation:**
  - **System.Security.Cryptography** (Windows DPAPI) — already built-in, use it
  - **System.Device.Tpm** (TPM 2.0) — official Microsoft library for TPM
  - **Action:** Implement DPAPI provider fully; audit file permissions on file-based storage

### Session & Pairing Tokens — `src/client/host/VideoForensics.Hosting/`
- **Files:** `SessionTokenService.cs`, `PairingTokenService.cs`, `StepUpAuthService.cs`
- **Status:** Using ASP.NET Core Data Protection API (secure)
- **Recommendation:** No change needed—Data Protection API is the standard for ASP.NET. Document revocation/invalidation behavior.

---

## 🟡 HIGH-PRIORITY REPLACEMENTS (Ready to Implement)

### 1. HTTP Client Retry & Rate Limiting — `src/providers/ring/core/HttpUtility.cs`
- **Current:** Custom throttling, ban detection, escalating backoff (30s→5min), hard 1-hour bans
- **Recommendation:** **Polly** (v8+) for retry/circuit-breaker patterns
- **Why:** Polly is battle-tested, standardized, and handles sophisticated scenarios (bulkhead, fallback, etc.)
- **Effort:** Medium
- **Scope:** Ring provider HTTP communication
- **Test:** Existing Ring integration tests cover rate-limit scenarios

### 2. Structured Logging — `src/core/core/core.logging/Services/ActionLogger.cs` + `FileLoggerProvider.cs`
- **Current:** Custom audit trail logging with file persistence
- **Recommendation:** **Serilog** + **Serilog.Sinks.File**
- **Why:** Structured logging, better performance, standard .NET ecosystem
- **Effort:** Low-Medium
- **Scope:** Audit logging (who-did-what-when events) for forensic compliance
- **Migrate:** ActionLogger → Serilog ILogger wrapper with `LogContext` for audit context

### 3. In-Memory Caching — `src/providers/ring/provider/Services/RingHistoryEventCache.cs` + multiple cache classes
- **Current:** Custom SemaphoreSlim-based caching with widening logic for Ring events
- **Recommendation:** **Microsoft.Extensions.Caching.Memory** (IMemoryCache) with **Polly.Caching** for cache-aside patterns
- **Why:** Built-in, standard, integrates with DI; Polly adds sophisticated cache invalidation
- **Effort:** Low
- **Scope:** 5+ cache implementations (RingHistoryEventCache, AuthAttemptCache, WebAuthnCeremonyCache, TwoFactorPendingAuthCache, CacheFreshnessService)
- **Test:** Existing cache behavior tests should still pass

### 4. Retry Policy — `src/providers/ring/provider/Services/RingRetryPolicy.cs`
- **Current:** Exponential backoff (1s→30s max, 3 attempts) with rate-limit vs. hard-ban distinction
- **Recommendation:** **Polly** AsyncPolicy for full retry + circuit-breaker
- **Why:** Consolidates with #1 (HttpUtility); avoids duplication
- **Effort:** Low
- **Scope:** Ring provider only (currently)

### 5. Date Parsing Robustness — `src/client/core/VideoForensics.Client.Core/Utilities/DateTimeUtilities.cs`
- **Current:** Custom multi-format date parsing (yyyy-MM-dd, M-d-yy, M/d/yy, MM/dd/yyyy, yyyy/MM/dd)
- **Recommendation:** **NodaTime** for more robust, culturally-aware date handling
- **Why:** Handles edge cases, time zones, and ambiguous dates better than DateTime
- **Effort:** Medium
- **Scope:** Client-side date input parsing (modest impact)
- **Trade-off:** Adds dependency; worth it if forensics work involves international dates

---

## 🟢 MEDIUM-PRIORITY REPLACEMENTS (Good Candidates)

### Testing Utilities & Builders
- **Current:** Custom mock handlers, test fixtures, builders
- **Recommendation:**
  - **Moq** (v4.20+) for mock objects → already in use, keep it
  - **Bogus** for fake data generation (replaces custom TestFixtures.cs)
  - **AutoFixture** for object creation in tests (replaces builder pattern)
- **Effort:** Low-Medium (test code only, no product impact)
- **Scope:** 
  - `src/providers/ring/core/tests/Mocks/TestFixtures.cs`
  - `src/providers/ring/video/metadata/tests/Fixtures/DoorbotHistoryEventBuilder.cs`
  - `src/providers/ring/snapshots/metadata/tests/Fixtures/SnapshotEventBuilder.cs`
- **Test:** Run test suite to verify builders are replaced correctly

### CLI Argument Parsing — `src/selftest/CliOptions.cs`
- **Current:** Custom CLI argument parsing
- **Recommendation:** **System.CommandLine** (Microsoft's modern library) or **CommandLineParser**
- **Effort:** Low
- **Scope:** Self-test CLI only
- **Note:** Only matters if selftest is customer-facing; low priority if internal-only

### IP Address Matching — `src/client/web/VideoForensics.WebApp/Services/BannedIpMatchService.cs`
- **Current:** Custom IP address ban matching/validation
- **Recommendation:** **IPNetwork** or **IpAddressRange** NuGet
- **Effort:** Low
- **Scope:** Security: IP ban enforcement (medium importance)

### Platform Directory Resolution — `src/core/helpers/core.helpers/Platform/PlatformDirectoryService.cs`
- **Current:** Custom cross-platform directory resolution (Windows CommonAppData, Linux XDG, macOS Library)
- **Recommendation:** Consider **System.IO.Abstractions** for better testability (wrapper pattern, not a full replacement)
- **Effort:** Low-Medium
- **Scope:** Storage location handling across platforms
- **Note:** Current implementation is adequate; main gain is testability

### mDNS Service Advertisement — `src/client/web/VideoForensics.WebApp/Discovery/MdnsAdvertisementService.cs`
- **Current:** Custom mDNS advertisement logic
- **Recommendation:** **Manatee.Mdns** or **Zeroconf** NuGet
- **Effort:** Medium
- **Scope:** Local network discovery (nice-to-have, not critical)
- **Test:** Integration test on test networks

### JSON Serialization Converters
- **Current:** Custom flexible type converters (FlexibleBooleanConverter, FlexibleIntConverter, etc.)
- **Recommendation:** Keep as-is (lightweight, specific to domain); document why each exists
- **Why:** These handle Ring/Wyze API quirks (API returns "1"/"0" for booleans, inconsistent types). Better to keep centralized.
- **Effort:** N/A (keep, don't replace)

---

## 🔵 LOW-PRIORITY / KEEP-AS-IS

### Forensics Domain Code (No NuGet Equivalent)
- EvidencePiiRedactor, EvidenceValidator, ForensicAnalyzer, ChainOfCustodyLogger, MultiDeviceForensics, ForensicReportSigner
- **Recommendation:** Keep as custom domain logic—no generic NuGet will match forensics-specific requirements
- **Note:** Document assumptions (e.g., what constitutes PII, what signatures are required)

### Provider-Specific Implementations
- Ring/Wyze/Uniview auth, device discovery, media download, event sync
- **Recommendation:** Keep as abstraction over provider APIs—already well-modeled
- **Note:** Provider-specific error classifiers (e.g., RingProviderApiErrorClassifier) are good as-is

### Entity Framework Data Access
- UnitOfWork, Repositories, Configurations
- **Recommendation:** Keep as-is—EF Core is the NuGet already; repository pattern is standard
- **Note:** 80+ repository files are appropriate for a domain-heavy application; no replacement needed

### ASP.NET/Blazor Infrastructure
- SignalR hubs, middleware, DI extensions
- **Recommendation:** Keep as-is—these are Framework integration points, not generic utilities

---

## 📊 Prioritization Matrix

| Package | File(s) | Effort | Risk | Impact | Priority |
|---------|---------|--------|------|--------|----------|
| **Polly** | HttpUtility.cs, RingRetryPolicy.cs | Medium | Low | High (standardizes retry logic) | 🔴 #1 |
| **Serilog** | ActionLogger.cs, FileLoggerProvider.cs | Medium | Low | High (audit compliance) | 🔴 #2 |
| **IMemoryCache** | RingHistoryEventCache.cs + 4 others | Low | Very Low | Medium (consolidates 5 caches) | 🟡 #3 |
| **Bogus** | Test fixtures & builders | Low | None | Low (test quality) | 🟡 #4 |
| **System.Security.Cryptography** | DpapiKeyStorageProvider.cs | Low | Very Low | Medium (complete key storage) | 🟡 #5 |
| **NodaTime** | DateTimeUtilities.cs | Medium | Low | Low-Medium (robustness) | 🟢 #6 |
| **System.CommandLine** | CliOptions.cs | Low | None | Low (if user-facing) | 🟢 #7 |
| **Manatee.Mdns** | MdnsAdvertisementService.cs | Medium | Low | Low (nice-to-have discovery) | 🟢 #8 |
| **IPNetwork** | BannedIpMatchService.cs | Low | Low | Medium (security) | 🟢 #9 |
| **NaCl** | AesEncryption.cs | Medium | Medium | Medium (security audit first) | 🔴 Audit |

---

## 🛠️ Implementation Roadmap

### Phase 1: Immediate (1-2 sprints)
1. Audit & implement DPAPI key storage (unblock non-Windows key management)
2. Replace RingHistoryEventCache with IMemoryCache
3. Add Bogus to test projects; migrate TestFixtures builders

### Phase 2: Short-term (2-4 sprints)
1. Integrate **Polly** for HttpUtility & RingRetryPolicy → consolidate retry logic
2. Migrate logging to **Serilog** for audit trail → improves compliance
3. Review AesEncryption implementation; consider NaCl adoption

### Phase 3: Medium-term (4-8 sprints)
1. Add **NodaTime** for date parsing robustness
2. Implement **System.IO.Abstractions** wrapper for PlatformDirectoryService
3. Replace mDNS advertisement with **Zeroconf**/Manatee.Mdns

### Phase 4: Polish (as-needed)
1. CLI argument parsing → **System.CommandLine**
2. IP matching → **IPNetwork** NuGet
3. Custom converters → document why they're needed

---

## ⚠️ Known Issues & Gaps

1. **DPAPI Provider Not Implemented** — Currently a placeholder; blocks Windows key storage
2. **TPM Provider Untested** — Windows/Linux platform parity unknown
3. **AesEncryption.cs** — Home-rolled; secure but should be formally audited
4. **No Circuit Breaker Logic** — Polly would add bulkhead/fallback capabilities (not currently implemented)
5. **No Centralized Metrics** — OpenTelemetryProvider exists but integration is sparse; consider **OpenTelemetry** NuGet

---

## 📋 Action Items for User

- [ ] Review security audit recommendations (AES, DPAPI, TPM)
- [ ] Prioritize Phase 1 replacements (IMemoryCache, Bogus, DPAPI)
- [ ] Evaluate NodaTime vs. current DateTimeUtilities (scope/cost-benefit)
- [ ] Decide on Polly adoption (impacts Ring provider HTTP layer)
- [ ] Plan Serilog migration (audit logging is critical path)
- [ ] Document why custom JSON converters are needed (for future maintainers)

---

**End of Audit Report**
