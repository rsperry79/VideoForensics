# Fail2ban-style auto-ban

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. Layers 1-4 of the lockout design are live (per-account lockout, GeoIP block, threat-intel block, manual `BannedIpRange` list); no cross-account IP correlation or automatic ban exists.

**Source**: `plans/archive/external-auth-smb-integration.md` line 67 ("Deferred fast-follow, not built now: fail2ban-style auto-ban ... natural next step once layers 1-4 are live and their false-positive rate is understood").

## Verdict

STILL OPEN. The prerequisites it was waiting on all exist on `origin/dev`: `BannedIpRange` entity (with nullable `ExpiresAtUtc` for temporary bans), `IBannedIpRangeRepository` (`GetActiveAsync`/`AddAsync`/remove), `IBannedIpMatchService` checked in the password login handler, and per-account lockout via `IOperatorRepository.IncrementFailedLoginAttemptAsync`. Nothing records which source IP triggered which lockout across accounts, and nothing writes a ban automatically. Grepped: `Lockout`, `FailedAttempt`, `LockedUntil`/`LockedOutUntilUtc`, `RateLimit`, `ForwardedHeaders`, `X-Forwarded`, `RemoteIpAddress`, `BannedIpRange`, `IBannedIpMatchService`, `fail2ban`, `IpBan` across `origin/dev` `src/**/*.cs`.

## Scope

In: an in-process failed-login tracker keyed by resolved client IP that counts distinct operator ids (and, optionally, distinct unknown usernames) failing within a window; when the threshold is crossed, add a temporary `BannedIpRange` (/32 or /128) with an expiry and audit it; admin-configurable thresholds on `LockoutPolicySettings`; unban via the existing Banned IPs UI.
Out: new ban storage tables, GeoIP/threat-intel changes, Cloudflare WAF push, passkey/LDAP paths beyond reusing the same hook where they already call the failed-attempt path, mobile layout.

## Design decisions

- **Counting state: in-memory; ban result: persisted.** Failure counters are short-lived (minutes), high-churn, and cheap to lose on restart (worst case an attacker gets one extra window). Use `IMemoryCache`/a `ConcurrentDictionary` with sliding expiry behind a new `IFailedLoginIpTracker` interface. The resulting ban is written as a `BannedIpRange` row via the existing repository so it survives restart, is visible in the Banned IPs UI, is audited, and is removable by an admin. No new tables or migration needed for v1 except settings columns (below). Single-server deployment assumed (SQLite); no distributed counter needed.
- **What counts as a trigger.** Count a distinct (IP, operator) pair when that operator transitions to locked out (the `RecordAccountLockoutAsync` branch in `OperatorAuthEndpoints.cs`), matching the source wording "triggers lockout across multiple different accounts". Default: 3 distinct accounts locked from one IP within 60 minutes -> ban for 24 hours. Add `AutoBanDistinctAccountThreshold`, `AutoBanWindowMinutes`, `AutoBanDurationMinutes`, `AutoBanEnabled` (default true, or false until false-positive rate is known) to `LockoutPolicySettings` (matches the doc's "once false-positive rate is understood" caution; default off is the safer rollout). Unknown-username failures are not counted for v1 (an attacker spraying nonexistent names never locks anyone; revisit as an open question).
- **Client IP and the proxy trust question.** Do NOT read `X-Forwarded-For`. Key on `INetworkTierResolver.ResolveClientIp(HttpContext)` in `src/client/host/VideoForensics.Hosting/NetworkTierResolver.cs`, which already trusts `CF-Connecting-IP` only when the TCP peer is a genuine hardcoded Cloudflare edge range and otherwise uses `RemoteIpAddress`. This is the same resolver the `auth` rate limiter in `Program.cs` uses, so ban keys and rate-limit buckets agree. Caveat: the hardcoded list is IPv4 only; an IPv6 Cloudflare edge peer falls back to the edge IP, which would make an auto-ban hit Cloudflare's shared edge. Mitigation: never auto-ban an address that `ResolveTier` classifies as Cloudflare edge/unresolved; log and skip.
- **Never ban Local/private by mistake.** Skip auto-ban when tier is `Local` (loopback) and when the resolved IP is "unknown". LAN (Network tier) private addresses are bannable but flagged in the audit entry; an admin on the same LAN behind NAT could ban the whole house, so ban only the exact /32 or /128, never a wider range.
- **Lockout interaction.** Tracker hook runs after `IncrementFailedLoginAttemptAsync` detects a new lockout. Ban check already runs before password verification in the login handler (`IsIpBannedAsync`), so a banned IP gets the same generic 401 and spends no hashing time. Per-account lockout is unchanged. A successful login does not clear the IP's tracker entry (an attacker could interleave their own valid account to reset counters); entries expire only by window.
- **Cache coherence.** `BannedIpMatchService` reloads active ranges from the DB on every call, so a new ban is effective immediately; no invalidation needed. Consider a short TTL cache later only if profiling shows cost.
- **Unbanning.** Reuse the existing Banned IPs section on `SecurityLockoutPolicy.razor` (remove entry). Add a `CreatedByOperatorId` convention for system bans (see open question: field is `required Guid`, so use a well-known system operator id/Guid.Empty and a `Reason` prefix like "Auto-ban: ..." so the UI can label them). Expired bans stop matching via `GetActiveAsync`; add a periodic purge only if table growth matters.
- **Audit.** Write a security audit entry on auto-ban creation (`ISecurityAuditService`/`SecurityAuditEventTypes`), human-readable, with the IP and the count of accounts; no raw account GUIDs in logs per CLAUDE.md (use counts/usernames-hash or "N accounts").

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/data/common/data.common/Entities/LockoutPolicySettings.cs` | Add `AutoBanEnabled`, `AutoBanDistinctAccountThreshold`, `AutoBanWindowMinutes`, `AutoBanDurationMinutes` | Admin-tunable policy |
| `src/data/database/data.database/Configurations/LockoutPolicySettingsConfiguration.cs` | Map new columns | Schema |
| `src/data/database/data.database/Repositories/LockoutPolicySettingsRepository.cs` | Defaults on create, copy fields on update | Persist policy |
| `src/data/database/sqlite/data.database.sqlite/Migrations/` | NEW migration adding the four columns | Schema change (the only existing migration is `20261008165211_InitialCreate`; confirm whether to add or regenerate) |
| `src/core/api/VideoForensics.Api.Contracts/LockoutPolicyDtos.cs` | Add fields to DTOs and `ToDto`/`ToDomain` | Wire contract |
| `src/client/web/VideoForensics.WebApp/Api/LockoutPolicyEndpoints.cs` | Accept/validate new fields (positive ints) | API |
| `src/client/host/VideoForensics.Hosting/Remote/RemoteLockoutPolicyService.cs` | Map new fields | Client host parity |
| `src/client/web/VideoForensics.WebApp/Services/IFailedLoginIpTracker.cs` | NEW interface (`RecordLockoutAsync(ip, operatorId, policy, ct)` returns whether a ban was created) | Public API as interface |
| `src/client/web/VideoForensics.WebApp/Services/FailedLoginIpTracker.cs` | NEW in-memory sliding-window distinct-account counter; writes `BannedIpRange` via `IBannedIpRangeRepository`, audits | Core logic |
| `src/client/web/VideoForensics.WebApp/Program.cs` | Register tracker as singleton (near the `IBannedIpMatchService` registration, ~line 299) | DI |
| `src/client/web/VideoForensics.WebApp/Api/OperatorAuthEndpoints.cs` | Call tracker in the lockout-detected branch (~line 660-665); inject `IFailedLoginIpTracker` | Hook point |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/SecurityLockoutPolicy.razor` (+ `.resx`) | Four settings inputs, "Auto" label on system bans; all strings localized via `L[...]` | Admin UI, localization rule |
| `src/client/web/VideoForensics.WebApp.Tests/FailedLoginIpTrackerTests.cs` | NEW | Tests |

## Test plan

Test-first; each test must fail first (missing type/behavior), then pass. Project: `src/client/web/VideoForensics.WebApp.Tests` (existing, already holds `BannedIpMatchServiceTests.cs`, `OperatorAuthEndpointsTests.cs`, `LockoutPolicyEndpointsTests.cs`); settings round-trip in `src/data/database/data.database.tests/Repositories/LockoutPolicySettingsRepositoryTests.cs`; DTO mapping in the existing lockout DTO/Remote tests under `src/client/host/VideoForensics.Hosting.Tests/RemoteLockoutPolicyServiceTests.cs`.

1. `FailedLoginIpTracker_ThresholdDistinctAccounts_CreatesTemporaryBan` (fails: type missing, then no ban written).
2. `FailedLoginIpTracker_SameAccountRepeated_DoesNotBan`.
3. `FailedLoginIpTracker_BelowThreshold_DoesNotBan`.
4. `FailedLoginIpTracker_EventsOutsideWindow_AreNotCounted` (use `FakeTimeProvider`/`TimeProvider` injection).
5. `FailedLoginIpTracker_AutoBanDisabled_NeverBans`.
6. `FailedLoginIpTracker_LoopbackOrUnknownIp_NeverBans`.
7. `FailedLoginIpTracker_BanCreated_SetsExpiryAndSlash32` (and /128 for IPv6).
8. `FailedLoginIpTracker_AlreadyBanned_DoesNotDuplicate`.
9. `FailedLoginIpTracker_ConcurrentRecords_CountsCorrectly`.
10. `OperatorAuthEndpoints_LockoutsFromOneIpAcrossAccounts_ThirdAttemptRejectedGenerically` (pipeline-style, mirroring `OperatorAuthEndpointsTests`); banned request returns the same generic 401.
11. `OperatorAuthEndpoints_SpoofedForwardedHeaderFromNonCloudflarePeer_IsNotUsedForBan` (verifies `X-Forwarded-For`/`CF-Connecting-IP` ignored).
12. Settings repository/endpoint/DTO round-trip tests for the four new fields and validation (zero/negative rejected).
13. Razor page test in `src/client/ui/VideoForensics.Ui.Shared.Tests/SecurityLockoutPolicyPageTests.cs` asserting localized labels.

Local run scoped with `dotnet test --filter` to these classes only.

## Risks and open questions

- False positives: shared NAT/office egress or a pen-test from one IP across many accounts bans everyone behind it for the duration; default-off plus audit plus easy unban mitigates. Decide default of `AutoBanEnabled`.
- Self-lockout DoS: an attacker who can reach the login endpoint from a spoofed-per-request source cannot be banned, but cannot ban others either, since the key is the TCP peer or a verified Cloudflare header. Behind a different reverse proxy (not Cloudflare) every client appears as the proxy IP and an auto-ban could ban the proxy, taking everyone out. Mitigation: refuse auto-ban when the peer is a configured trusted-proxy address; a `ForwardedHeaders` trusted-proxy allowlist does not exist on dev, so v1 documents "no non-Cloudflare reverse proxies supported" and skips private-range /32 bans only if tier is Network and the peer is not the sole observed source (open: confirm desired behavior).
- IPv6 Cloudflare edge ranges are not in `NetworkTierResolver`'s list; fix or skip-ban guard needed.
- `BannedIpRange.CreatedByOperatorId` is `required Guid` with an FK expectation to be checked in `BannedIpRangeConfiguration`; a system actor may need a seeded system operator or nullable column (migration).
- In-memory counters reset on restart and are per-process; acceptable for a single-server design.
- Unknown-username spray and passkey/LDAP failure paths are not counted in v1.
- Migration strategy: only `InitialCreate` exists; confirm add-new-migration vs. regenerate.

## Priority recommendation

**Low.** Product value is modest and security exposure is already covered by per-account lockout, the `auth` sliding-window rate limiter (5 requests per 15 minutes per resolved client IP, Network/Internet tiers), GeoIP, threat-intel, manual bans and Cloudflare WAF; the main gap is slow multi-account spraying, and the feature itself adds false-positive and proxy-ban risk. Effort: **M** (new tracker, four settings columns across entity/DTO/endpoint/remote/UI/migration, plus tests).

## Implementation dispatch

Test-first in three Sonnet subagent batches: (1) settings fields end to end (entity, config, repo, migration, DTOs, endpoint, remote service) with their tests; (2) `IFailedLoginIpTracker`/`FailedLoginIpTracker` plus DI and the `OperatorAuthEndpoints` hook, tests written and confirmed failing first; (3) `SecurityLockoutPolicy.razor` inputs, `.resx` keys and page tests; run the lite gate after each batch.
