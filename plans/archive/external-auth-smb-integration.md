# External Auth Providers, SuperAdmin Toggle, and SMB Group Integration

## Context

VideoForensics currently authenticates operators only via local username/password or WebAuthn passkeys (`OperatorAuthEndpoints.cs`), issued through a single custom `PairedDevice` auth scheme (not cookie/JWT). Separately, the Inno Setup installer (`deploy/windows/VideoForensics.iss`) can optionally share the Reports/Media folders over SMB, gated by two local Windows groups (`VideoForensicsAdmin`/`VideoForensicsSuperUser`) — but only the installing user is ever added to them, and the installer's own `TODO(user-management)` comment calls out that membership needs a real in-app admin screen, not an installer-time hack.

The user wants to investigate and add support for external identity providers — **Microsoft Account (MSA), Google, and on-prem Active Directory** (Azure AD/Entra ID explicitly out of scope — the org doesn't use it) — for both **app login** and, where it's not disproportionately complex, **SMB share group membership**. A SuperAdmin page should let an admin enable/disable which providers are active. Any externally-authenticated user must still go through admin approval before getting real access — mirroring the self-service registration gate (`Operator.IsApproved`) that already exists today.

Research confirmed: on-prem AD is the only provider that plugs into SMB group membership without disproportionate complexity (native `net localgroup DOMAIN\user`); MSA/Google have no viable SMB integration path and must instead show a clear notice in the SMB admin UI. For app login, all three are feasible: MSA/Google via OIDC, on-prem AD via LDAP bind (not full Kerberos SSO, to avoid an IIS/Kerberos dependency on a self-hosted Kestrel app).

This is a 5-phase build. Each phase is independently shippable and passes CLAUDE.md's lite gate (incremental `dotnet build`/`dotnet test` on touched projects) before moving to the next. The full gate (clean rebuild + package updates + full test suite) only runs before a PR, and only after user confirmation, per CLAUDE.md.

**Alternative designs considered and rejected (documented so they aren't re-litigated mid-build):**
- **Windows Integrated Auth (Kerberos/Negotiate) instead of LDAP bind for on-prem AD** — `Microsoft.AspNetCore.Authentication.Negotiate` works directly on Kestrel (no IIS needed) and would give transparent SSO with no password ever touching the app, simpler than LDAP bind's manual credential form + LDAPS cert management. Rejected because it requires the **server** machine to be domain-joined, which isn't guaranteed across deployments — LDAP bind has no such requirement (just network reachability to a DC), so it's the design that works everywhere. Phase 4 stays LDAP bind.
- **Client-side OIDC token verification instead of full server-side redirect flow** for MSA/Google — would avoid the "restart required after enabling a provider" limitation and the server-side callback/state-correlation logic, at the cost of Blazor JS interop complexity and a small static JS page needed inside MAUI's system-browser flow. Rejected in favor of the more conventional, better-trodden full server-side OIDC redirect flow (`AddOpenIdConnect`) already detailed in phase 3 — keep the documented restart-after-enable limitation as a known, acceptable tradeoff.

**Redirect URI resolution (resolved).** OAuth/OIDC requires an exact, pre-registered redirect URI — it can't float with whatever address the browser happens to be on. Per the user: lock external login to exactly two fixed hostnames, register both as redirect URIs with each IdP, and only show "Sign in with…" buttons when the request arrives on one of those two hosts (anything else — raw IP, `localhost`, an unrecognized LAN address — falls back to password/passkey only, no silent failure). Concretely:
- **Internet name**: reuse the existing `InternetServerUrl` config (`ConfigDtos.cs`) / `ICloudflaredTunnelService.PublicUrl` — don't add a second field for this, it already exists.
- **Local network name**: this is new, and **cannot reuse mDNS** (`MdnsAdvertisementService.cs`'s `.local` advertisement, used for MAUI/CLI server discovery) — `.local` names are reserved for mDNS by RFC 6762, and CA/Browser Forum baseline requirements ban publicly-trusted CAs from issuing TLS certificates for `.local`/internal names, while OAuth providers require HTTPS redirect URIs. mDNS discovery stays completely untouched by this plan; the OIDC local hostname is a separate mechanism. Add one admin-set field (`AuthProviderRedirectSettings.LocalNetworkHostname`, a small server-side singleton config row, separate from `AuthProviderConfig` since it's shared across all providers) — a real, owned DNS name (a subdomain of a domain the admin controls) pointed at the LAN server via internal/split-horizon DNS, with a certificate obtained via ACME DNS-01 challenge (only needs a TXT record to prove domain ownership, not public HTTP reachability — works fine for an internal-only hostname). Document in the UI that this must be a stable name pointed at a fixed LAN address, not a DHCP-assigned IP, or external login silently stops working after an IP change.
- **MAUI/mDNS unaffected**: MAUI's server discovery continues to use mDNS exactly as today, unrelated to any of this. LDAP and password-entry login forms are WebApp-browser-only — MAUI's auth stays device-pairing/passkey (existing) plus the new external-OIDC device-code flow (phase 3); no native LDAP form gets built inside MAUI.

## Threat model note (drives several defaults below, not just this section)

This app is used in high-stakes personal-safety contexts, including by domestic-violence survivors/advocates — the adversary can be a specific, personally-motivated, technically-capable individual (a "high value target" scenario), not just an opportunistic internet attacker. This changes several assumptions made elsewhere in this document:
- **LAN/local-network origin is not a trust signal.** An abuser frequently shares the household, the Wi-Fi, sometimes the devices. `NetworkTier.Local`/`SuperAdminLocal` remain useful as "hard to reach remotely," but lockout, 2FA, and audit logging apply uniformly regardless of network tier — never relaxed for "local" traffic.
- **External IdP login is a new attack surface, not a neutral convenience.** Google/Microsoft accounts are commonly shared, family-managed, or accessible to an abuser (shared Family account, known password, physical device access). `AuthProviderConfig.IsEnabledForLogin` defaults to **false** for every external provider; enabling one shows an explicit warning about shared-account risk before the SuperAdmin confirms.
- **No self-service password/account recovery, ever** — no email-based "forgot password" flow gets added anywhere in this plan. That's a classic DV compromise vector when the abuser controls or monitors the victim's email/phone. Account recovery stays an explicit SuperAdmin action.
- **Lockout can be weaponized against the account holder** — an abuser can deliberately fail logins to lock the victim out as denial-of-service. Keep lockout duration short by default (15 min) and treat the SuperAdmin-manual-unlock release valve as load-bearing, not incidental — it only works as a release valve if the approving SuperAdmin isn't the same household (a deployment/operational note, not something code can enforce).
- **SMB sharing's "opt-in every time, never sticky" installer posture (`Flags: unchecked`, no `checkedonce`) is a deliberate safety property**, not an oversight — the new in-app SMB admin screen (phase 5) must preserve it, not make sharing more convenient or persistent, and should carry the same warning about LAN-level evidence exposure.
- **Breach/login-attempt data has evidentiary value, not just operational value** — "is someone trying to get into my account" is a safety question with a real answer, and a documented pattern of attempts could matter for a protective order. This is why the MCP-exposed breach-analysis capability (phase 0.5, below) ships right after the audit data exists, not as a last capstone.

## Phasing & rationale

0. **Break-glass primary SuperAdmin + account lockout policy + 2FA policy** — hardening the *existing* password-only system, independent of whether any external-auth phase below ever ships. **Split out as its own immediately-shippable deliverable** — don't gate it on the rest of this epic.
0.5. **Login/breach-attempt visibility via MCP** — self-service "my own login/lockout history" + admin-scoped cross-account security-event query, exposed as a server-side MCP tool the existing `VideoForensics.Mcp` proxy forwards unchanged. Ships as soon as phase 0's audit data exists; extended incrementally as later phases add more event types. Moved up from a capstone to right after phase 0 because "is someone trying to get into my account" is a safety feature, not an analytics nice-to-have.
1. **Schema + SuperAdmin toggle/config page** — everything else depends on this.
2. **External-identity linkage + admin-approval workflow** — built and tested against a manually-inserted fake identity, before any real IdP exists. This is deliberately ordered before OIDC/LDAP so each later phase only adds *one* new thing (a real handshake) against an already-tested approval path, rather than building and testing approval under the pressure of a first live IdP integration.
3. **OIDC login wiring** (MSA, Google) — off by default per the threat-model note above; enabling one is a deliberate, warned-about admin action.
4. **LDAP bind** for on-prem AD app login.
5. **SMB share group-membership admin screen** (AD-only integration; notice for the rest) — last because it's the most isolated and lowest-risk-to-defer.

---

## Phase 0 — Break-glass primary SuperAdmin + account lockout policy

**Primary SuperAdmin stays local-only, permanently (resolved decision).** The bootstrap SuperAdmin created via `/setup` must always remain reachable even if every external provider is misconfigured, disabled, or the app is Internet-exposed — a permanent break-glass account. Additional SuperAdmins may be provider-bound (an external identity approved at `SuperAdmin` role); only the one primary account is restricted this way.
- `Operator.cs` gains `IsPrimarySuperAdmin` (bool, default false) — set `true` only by `SetupEndpoints.cs`'s `/api/v1/setup/create-admin` bootstrap path, never assignable afterward (no "promote to primary" flow; there is exactly one, ever, per install).
- `OperatorAuthEndpoints.cs`'s password-login handler: when the target operator has `IsPrimarySuperAdmin = true`, reject the login attempt outright unless the request's resolved `NetworkTier` is `Local` (loopback) — this is a **login-time** check, not just a later authorization-policy check on actions, so a correct password from a LAN or Internet-tier request never authenticates this specific account.
- Phase 2's approval/linking endpoints must refuse to attach any `OperatorExternalIdentity` to an operator with `IsPrimarySuperAdmin = true` — add this guard now so phase 2 doesn't need to retrofit it.

**Account lockout policy (new capability — doesn't exist in the codebase today).** Directly motivated by adding externally-reachable login surface: password and (later) LDAP login both accept user-supplied credentials and need brute-force protection beyond the existing IP-based `"auth"` rate limit (which throttles by IP, not by account — the two are complementary, not redundant).
- `Operator.cs` gains `FailedLoginAttemptCount` (int, default 0) and `LockedOutUntilUtc` (DateTime?, nullable) — same minimal-column-migration pattern as `AddOperatorIsApproved`.
- New singleton config `LockoutPolicySettings` (`MaxFailedAttempts` default 5, `LockoutDurationMinutes` default 15, `UpdatedAtUtc`/`UpdatedByOperatorId`) — SuperAdminLocal-gated.
- `OperatorAuthEndpoints.cs` password-login handler: check `LockedOutUntilUtc` before validating the password; on failure increment `FailedLoginAttemptCount` and set `LockedOutUntilUtc` once the configured threshold is hit; on success reset the counter to 0. Phase 4's LDAP login endpoint reuses the exact same helper — write it generically now.
- **Failure responses stay generic regardless of cause** ("invalid credentials" whether the username doesn't exist, the password is wrong, or the account is locked) — distinguishing them in the response enables username enumeration and lockout-probing. Lockout status is only ever visible to the account owner after a *successful* auth attempt is blocked, or to a SuperAdmin.
- Lockout is scoped to password + LDAP login only — WebAuthn passkey assertions aren't meaningfully brute-forceable and don't participate.
- SuperAdmin manual-unlock: `POST /devices-management/operators/{id}/unlock` in `DeviceManagementEndpoints.cs`, `SuperAdminLocal`, audit-logged (no step-up — this restores existing access rather than granting new access, unlike approve/deny).
- **UI:** new `src/client/ui/VideoForensics.Ui.Shared/Pages/SecurityLockoutPolicy.razor`, route `/settings/lockout-policy`, following the `SecurityOperators.razor`/`NetworkSettings.razor` pattern — configure `MaxFailedAttempts`/`LockoutDurationMinutes`, and an unlock action on any currently-locked operator.
- **Audit:** lockout triggers and manual unlocks both get `ISecurityAuditLogger` entries.

**Geo/IP-reputation lockout (two layers, deliberately not one big custom thing):**
- **Layer 1 — Cloudflare Edge WAF, zero app code.** Confirmed `CloudflaredTunnelService.cs` only wraps the `cloudflared` CLI (via CliWrap) — it has no Cloudflare API token or WAF/zone management wired up today, so "extend it to push firewall rules" would be net-new integration work, not free reuse. Since Internet-tier traffic already transits a Cloudflare Tunnel, Cloudflare's own free WAF (country-block rules, bot/IP-reputation scoring) can block hostile traffic *before it reaches Kestrel at all* — the least-code option for exactly the traffic that matters most. Ship this as a documented one-time setup step (a link from `RemoteAccess.razor` to Cloudflare's WAF dashboard for this zone), not app code.
- **Layer 2 — app-level country lockout via MaxMind GeoLite2**, for LAN/Network-tier traffic that never touches Cloudflare. Add `IGeoIpLookupService`/`MaxMindGeoIpLookupService` (server-only) using the official `MaxMind.GeoIP2` + `MaxMind.Db` NuGet packages (actively maintained — confirmed updated mid-2026) against the free GeoLite2 country database. Extend `LockoutPolicySettings` with `BlockedCountryCodes` (ISO 3166-1 alpha-2 list, admin-configurable on the same lockout policy settings page) and check it in the same login pipeline as the failed-attempt lockout, before credential validation — same generic failure response either way, no enumeration. **Fail open, not closed**, if the GeoLite2 DB is missing/stale/unreachable (log via `GetLastError()`): a security control that can lock everyone out because a background DB refresh failed is worse than temporarily missing one layer, especially since per-account lockout still applies regardless. Needs a free MaxMind license key (operational prerequisite, not code) and a periodic background job to refresh the DB file under the app's ProgramData data directory — that refresh job is the one piece of genuine ongoing maintenance a NuGet package doesn't remove.
- **Layer 3 — pullable threat-intel IP blocklist.** Free, no-API-key CIDR/IP feeds: **Spamhaus DROP/EDROP** and **Emerging Threats' compromised-IPs list**, both plain-text, pulled periodically by a background job (same refresh pattern as the GeoLite2 DB) and cached under ProgramData. Matching uses **`System.Net.IPNetwork`**, built into the .NET 8+ BCL — no new NuGet needed just for CIDR containment checks. (Note: the GitHub "SecLists" project is a pentesting wordlist collection, not a live malicious-IP feed — not the right tool here.) Same fail-open-on-fetch-failure behavior as layer 2 by default.
- **Fail-open vs. fail-closed on lookup failure is a config toggle, not hardcoded** (`LockoutPolicySettings.FailClosedOnLookupError`, default `false`/fail-open). A generic install shouldn't risk locking everyone out because a background DB refresh failed; a high-value-target deployment may judge availability loss the lesser risk and choose fail-closed instead — that's a call the deployment should get to make, not one baked into the code.
- **Layer 4 — user-configurable manual ban list.** New `BannedIpRange` entity (`CidrRange`, `Reason`, `CreatedAtUtc`, `CreatedByOperatorId`, `ExpiresAtUtc` nullable for temporary bans), `IBannedIpRangeRepository`, checked in the same login-pipeline gate as layers 2/3. Manage it as a "Banned IPs" section on the same `SecurityLockoutPolicy.razor` page — add/remove a CIDR range with a reason, optional expiry.
- **Deferred fast-follow, not built now**: fail2ban-style auto-ban — an IP that triggers lockout across multiple *different* accounts within a time window automatically gets a temporary entry in layer 4's ban list. This adds real cross-account correlation logic beyond what's being asked for now; flag it as a natural next step once layers 1-4 are live and their false-positive rate is understood.

**Tests:** `Operator` login-handler tests (primary SuperAdmin rejected at Network/Internet tier even with correct password; lockout triggers at threshold, resets on success, generic error message in all failure cases; blocked-country request rejected generically; GeoLite2/threat-intel-feed lookup failure fails open with a logged error; banned-CIDR match rejected generically), `LockoutPolicySettings` repository/endpoint tests, unlock-endpoint tests, `MaxMindGeoIpLookupServiceTests.cs` (mocked reader, not a real DB file in unit tests), `ThreatIntelBlocklistServiceTests.cs` (mocked feed fetch), `BannedIpRangeRepositoryTests.cs`.

**NuGet:** `MaxMind.GeoIP2`, `MaxMind.Db`. (Layer 3's CIDR matching uses the BCL `System.Net.IPNetwork` — no package needed for that part.)

**Configurable two-factor requirement, granular by role and by individual operator.** This generalizes and replaces the earlier ad-hoc "Admin/SuperAdmin must have a passkey" rule (from the step-up gap in phase 2) into one real policy, rather than leaving two mechanisms saying almost the same thing.
- `TwoFactorRoleRequirement` — a small config table with one row per `OperatorRole` (`ReadOnly`/`Review`/`Admin`/`SuperAdmin`), each with a `RequireTwoFactor` bool. Modeled as a per-role map rather than a single "minimum role" threshold, since a flat threshold can't express "granularity" as literally asked — an install may reasonably want 2FA required for `Review` and above but not `ReadOnly`, for example. **Defaults: required for every role, all four**, given the threat-model note above — this app's typical high-stakes use case argues for secure-by-default rather than "convenient by default, strict only for admins." An install that genuinely needs to relax it for low-sensitivity `ReadOnly` access can still do so per-role.
- `Operator.cs` gains `TwoFactorRequirementOverride` (nullable enum: `Inherit` (default) / `Required` / `NotRequired`) — lets a SuperAdmin force the policy on or off for one specific person regardless of role.
- **Kept deliberately simple — reuse what exists, don't invent a new auth state machine.** A standalone WebAuthn passkey sign-in always satisfies the policy on its own, full stop — no configurable flag for this, it's definitional (a hardware-backed passkey already is a strong authenticator). For password login, external IdP login, or LDAP bind, when the policy requires a second factor: reuse the **existing** standalone-WebAuthn-sign-in assertion endpoints (`/api/v1/auth/webauthn/username-assertion-options`/`-complete`, already built for passwordless sign-in) as the second step, rather than building a new pending-auth-token primitive. The only new plumbing is one opaque, short-lived correlation ID returned from the first successful factor, passed into the existing assertion-complete call so the server can confirm both steps belong to the same login attempt for the same operator before minting the real `ISessionTokenService` session. (Providers *may* include an `amr` claim indicating upstream MFA already happened, but it isn't reliably populated across providers/configs — not trusted as authoritative here; the local passkey step is what's actually enforced.)
- **Bootstrap/grace path, not a hard lockout on policy change:** if an operator newly subject to the requirement (role change, or a new per-user override) has zero registered `OperatorCredential` rows, force-redirect them to passkey registration on next login — reuse the exact same forced-flow pattern `AuthGate.razor` already uses for `MustChangePassword`, don't invent a second mechanism. This also covers the very first bootstrap SuperAdmin, who can't have a passkey at the literal moment of `/setup`.
- **UI:** role-level grid (4 rows) on `SecurityLockoutPolicy.razor` (or promote to its own `SecurityTwoFactorPolicy.razor` if that page gets crowded — implementer's call) plus a per-operator override control on the existing `OperatorDetails.razor` page, since the override is naturally an attribute of one specific operator, not a global list.
- **Audit:** policy changes (role-level or per-operator override) and every skipped/failed second-factor attempt get `ISecurityAuditLogger` entries.

**Tests (additional):** role-requirement + per-operator-override resolution tests (override always wins over role default), pending-auth-token tests (single-use, ~5-minute expiry, can't be reused to mint a second session), bootstrap-grace-path test (newly-required operator with no passkey gets redirected to registration, not locked out), one test per login method confirming factor-2 is actually enforced when required and skipped when not.

**Lite gate:** `data.common`, `data.database`, `data.database.sqlite`, `VideoForensics.WebApp`, `VideoForensics.Hosting`, `VideoForensics.Ui.Shared` + their test projects.

---

## Phase 0.5 — Login/breach-attempt visibility via MCP

**Motivation (see threat-model note):** "is someone trying to get into my account" is a safety question, not an analytics feature, and a documented pattern of attempts could matter as evidence. Ships as soon as phase 0's audit data exists — doesn't need any of the external-provider phases.

**No changes to `VideoForensics.Mcp` itself.** It's confirmed to be a pure stdio↔HTTP proxy that forwards every MCP tool call to the server's `/mcp` endpoint unchanged — the new capability is entirely a new **server-side MCP tool** registered alongside whatever existing tools the WebApp's `/mcp` endpoint already exposes; `VideoForensics.Mcp` picks it up automatically without any code of its own, exactly like every other tool it proxies.

**Two variants, not one, because the sensitivity differs:**
- `my_login_security_events` — any authenticated operator can query their **own** login/lockout/2FA history (self-service, low sensitivity, reassuring in exactly the DV scenario this is motivated by — "did someone try to log in as me").
- `security_login_events` — cross-account query across all operators, gated `SuperAdminLocal` (full admin visibility, physically-local-only, same bar as other infrastructure-exposure screens).

Both return **structured event data** (event type, operator ID/username if known, source IP + User-Agent, timestamp, result — failed_password, lockout_triggered, lockout_unlocked, geo_blocked, threat_intel_blocked, banned_ip_blocked, two_factor_failed, external_identity_denied), reading from whatever structured store `ISecurityAuditLogger`/`SecurityAuditEventTypes` already writes to (confirm at implementation time that this is genuinely structured/queryable, not just formatted log text — if it's the latter today, the phase 0 audit calls for login events need to carry structured fields so this tool has something real to query). Pattern analysis/summarization is left to the calling LLM (the whole point of exposing it via MCP) — this tool returns filtered raw data (time range, operator, event-type filters), not a bespoke analytics engine.

**Tests:** tool-handler tests for both variants (self-service scoped correctly to the caller's own operator ID; cross-account variant enforces `SuperAdminLocal`; filters work), data-shape tests confirming no secrets/tokens ever appear in a returned event.

**NuGet:** none new.

**Lite gate:** `VideoForensics.WebApp` + wherever the server's `/mcp` tool registrations live + their test projects.

---

## Phase 1 — `AuthProviderConfig` schema + SuperAdmin toggle/config page

**Entity/data layer** (follow `Operator`/`OperatorCredential` conventions):
- `src/data/common/data.common/Entities/AuthProviderConfig.cs` — `Id`, `ProviderType` (new enum `Msa`/`Google`/`OnPremAd`, colocated like `NetworkTier` is in `OperatorRole.cs`), `IsEnabledForLogin` (**default `false`** for every provider, per the threat-model note — enabling one is a deliberate admin action, not an automatic consequence of configuring it), `IsEnabledForSmb`, `DisplayName`, non-secret config (`TenantId`, `ClientId`, `Authority`/`LdapServerUrl`, `LdapBaseDn`), `SecretRef` (env-var name holding the client secret/bind password — **never** a plaintext secret column, per CLAUDE.md "no secrets in code"), `CreatedAtUtc`/`UpdatedAtUtc`/`UpdatedByOperatorId`.
- `src/data/database/data.database/Configurations/AuthProviderConfigConfiguration.cs` (`IEntityTypeConfiguration<AuthProviderConfig>`, unique index on `ProviderType`).
- `DbSet<AuthProviderConfig>` on `VideoForensicsDbContext`; `dotnet ef migrations add AddAuthProviderConfig` in `data.database.sqlite`.
- `IAuthProviderConfigRepository`/`AuthProviderConfigRepository` in `data.database/Repositories/` (`GetAllAsync`, `GetByTypeAsync`, `UpsertAsync`).
- `AuthProviderRedirectSettings` — a small singleton config row (`LocalNetworkHostname`, `UpdatedAtUtc`) for the OIDC redirect-URI local hostname (see "Redirect URI resolution" above); the Internet side reuses the existing `InternetServerUrl`/`ICloudflaredTunnelService.PublicUrl`, no new field needed for that half.

**Contracts:** `src/core/api/VideoForensics.Api.Contracts/AuthProviderDtos.cs` — `AuthProviderConfigDto` record (full XML `<param>` docs) + `AuthProviderConfigDtoMapping` (`ToDto()`/`ToDomain()`, matching `ProviderAccountDto`'s shape), `UpdateAuthProviderConfigRequest`.

**Server endpoints:** `src/client/web/VideoForensics.WebApp/Api/AuthProviderEndpoints.cs` — `MapGroup("/api/v1/auth-providers").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)`: `GET /` (list), `PUT /{providerType}` (mutating → `.AddEndpointFilter<StepUpEndpointFilter>()`, audit-logged via `ISecurityAuditLogger`, matching `NetworkSettingsEndpoints.cs`/`DeviceManagementEndpoints.cs`). **Disabling a provider that operators depend on for their only login method must warn, not silently lock people out**: before flipping `IsEnabledForLogin` off, the endpoint counts operators whose only `OperatorExternalIdentity`/credential is that provider (added once phase 2's `OperatorExternalIdentity` exists — phase 1 ships the toggle without this check, phase 2 backfills it) and the UI surfaces that count for confirmation.

**Client:** `IAuthProviderConfigService` + `RemoteAuthProviderConfigService` in `VideoForensics.Hosting/Remote/`, registered as one more `AddHttpClient<...>()` line in `AddVideoForensicsClientApi()` (`VideoForensicsHostingExtensions.cs`) — auth is automatic via the already-registered `PairedDeviceAuthHandler`, forward `CancellationToken` throughout.

**UI:** `src/client/ui/VideoForensics.Ui.Shared/Pages/AuthProviders.razor`, route `/settings/auth-providers`, linked from `Settings.razor`. Pre-check `PairedSessionState.IsSignedIn`/`Role`, catch 401/403 into `_authError` — matches `NetworkSettings.razor`/`SecurityOperators.razor`, not Blazor `[Authorize]`. Toggling `IsEnabledForLogin` to true for `Msa`/`Google` shows a confirmation warning first: "Microsoft/Google accounts are often shared or accessible to other household or family members. Only enable this for operators who have exclusive, secured control of that account" — a real click-through, not just a tooltip, given the threat-model note above.

**Tests (write first):** `AuthProviderConfigRepositoryTests.cs`, `AuthProviderEndpoints` tests (incl. 403 without `SuperAdminLocal`, step-up required on `PUT`), `RemoteAuthProviderConfigServiceTests.cs` (CancellationToken forwarding, auth header present) — mirror existing `Remote*Tests.cs` shape.

**NuGet:** none new.

---

## Phase 2 — External-identity linkage + admin-approval workflow

**Data layer:**
- `src/data/common/data.common/Entities/OperatorExternalIdentity.cs` — `Id`, `OperatorId` (nullable until approved+linked), `ProviderType`, `ExternalSubjectId`, `ExternalEmail`, `CreatedAtUtc`, `LastLoginAtUtc`, `IsApproved`, `ApprovedAtUtc`, `ApprovedByOperatorId`.
- `OperatorExternalIdentityConfiguration.cs` (unique index `(ProviderType, ExternalSubjectId)`).
- Migration: `AddOperatorExternalIdentity`.
- `IOperatorExternalIdentityRepository`/`OperatorExternalIdentityRepository`.

**Contracts:** `PendingExternalIdentityDto`, `ApproveExternalIdentityRequest` (carries the role to assign on approval) added to `AuthProviderDtos.cs`.

**Server endpoints:** extend `DeviceManagementEndpoints.cs` (same file, not a new one — mirrors the existing `operator-credentials/pending` sub-flow): `GET /devices-management/external-identities/pending`, `POST /devices-management/external-identities/{id}/approve` (assigns role, flips `Operator.IsApproved`), `POST /devices-management/external-identities/{id}/deny` (hard-deletes the pending identity row — no soft-reject state exists elsewhere in the schema, so don't invent one). Both mutating routes: `SuperAdminLocal` + step-up + audit log.

**Client:** `RemoteExternalIdentityService.cs` in `Hosting/Remote/`.

**UI:** extend `SecurityOperators.razor` with a "Pending External Identities" grid, same shape as the existing "Pending Credentials" grid — not a new page, since it's the same admin surface. Each pending row that has a matching-email existing `Operator` (see phase 3's callback logic) shows a "link to existing operator" option alongside "approve as new," so admins don't accidentally fork one person's identity into two accounts.

**Notifications:** reuse the existing one-shot notification pattern (`Operator.ApprovalFirstLoginNotifiedAtUtc`) for pending external identities too — a SuperAdmin needs to actually be told a pending external identity is waiting, not discover it by manually checking the page. Add the equivalent flag/mechanism to `OperatorExternalIdentity` (or reuse the existing notification path if it's generic enough) so it's not a silently-growing queue.

**Step-up credential requirement (important, easy to miss):** `SuperAdminLocal`/high-risk actions (including the approve/deny endpoints this phase adds) require a fresh WebAuthn passkey assertion via `StepUpEndpointFilter`. An operator whose *only* credential is an external IdP has no passkey and could never actually perform the approval action the feature exists for. **This is now handled by phase 0's configurable two-factor policy rather than a separate hardcoded rule**: phase 0's default `TwoFactorRoleRequirement` already requires `Admin`/`SuperAdmin` to have a registered passkey, and its bootstrap/grace-path forces passkey registration on next login for anyone newly subject to the requirement — the approve-endpoint's role-assignment path just needs to trigger that same grace path when assigning `Admin`/`SuperAdmin` to an operator with zero `OperatorCredential` rows, not implement a second, parallel check. Don't build a second, OIDC-based step-up mechanism; step-up stays the single hardware-backed mechanism it already is.

**Audit detail:** this is a forensics/evidence-handling app — the `ISecurityAuditLogger` entry for external-identity approval/deny/link must record enough to survive a later chain-of-custody question (provider, external subject ID, email, approving SuperAdmin, timestamp), not just "operator approved."

**No location/device-health signal from any provider (confirmed, not assumed):** Google/MSA standard OIDC ID tokens and a plain LDAP bind carry no location or device-health data — that requires Google Workspace Admin SDK access, Entra ID Conditional Access, or Kerberos-based Windows Event Log correlation respectively, none of which are in scope (Entra ID and Negotiate/Kerberos were both explicitly rejected above). The practical substitute, available for free: capture source IP + User-Agent on every login attempt — password, LDAP, and external-provider callback alike — into the audit log entry. This is server-observed, not IdP-reported, but it's the only "where did this login come from" signal actually available in this design; add it to every login-path audit entry across phases 0/3/4, not just external ones.

**Tests:** repository tests, endpoint tests (approve path sets role + `IsApproved`; deny path removes the row and leaves no `Operator` created; link-to-existing path attaches the identity to the matched `Operator` instead of creating a new one), `Remote*` tests. Check whether any `.razor` component test framework (bUnit) is already set up before adding one for `SecurityOperators.razor` — if not, cover the logic via endpoint tests only rather than introducing a new test framework for this alone.

**NuGet:** none new.

---

## Phase 3 — OIDC login wiring (MSA, Google)

**Server:**
- `src/client/web/VideoForensics.WebApp/Auth/ExternalOidcSchemeRegistrar.cs` — reads enabled `AuthProviderConfig` rows at startup and registers one OIDC scheme per enabled provider (`Microsoft.Identity.Web` or vanilla `AddOpenIdConnect` against `login.microsoftonline.com/consumers` for MSA — personal Microsoft accounts only, no org/tenant concept needed since Azure AD is out of scope; `AddOpenIdConnect` against Google's discovery doc). Registered in `Program.cs`. **Document as a phase-3 limitation:** provider list is read once at startup — enabling/disabling a provider requires an app restart to take effect (hot-reloadable schemes are explicitly deferred, see below). **Registration must be defensive**: wrap each provider's registration in try/catch so a bad config (malformed authority, wrong client ID) skips just that provider with a logged error (`GetLastError()` convention) rather than crashing WebApp startup.
- `src/client/web/VideoForensics.WebApp/Api/ExternalAuthEndpoints.cs`:
  - `GET /api/v1/auth-providers/public` — **anonymous**, unauthenticated, returns only `ProviderType` + `DisplayName` for rows where `IsEnabledForLogin = true`. This is what the login page uses to decide which "Sign in with…" buttons to show; it must never expose `ClientId`/`TenantId`/`SecretRef` to anonymous callers (that stays behind the phase-1 `SuperAdminLocal` endpoint).
  - `GET /api/v1/auth/external/{provider}/challenge` — 302 to IdP. Re-checks `AuthProviderConfig.IsEnabledForLogin` at request time (not just at startup registration) so disabling a provider blocks new logins immediately even though the OIDC scheme itself stays registered until restart. **Also rejects the challenge (redirect to an explanatory error, not a silent no-op) unless the incoming request's `Host` matches either `AuthProviderRedirectSettings.LocalNetworkHostname` or the configured `InternetServerUrl` host** — this is what keeps the redirect URI registered with Google/MSA always valid; the login page correspondingly hides "Sign in with…" buttons when the current host doesn't match either. The `state`/correlation value passed through the handshake must be a freshly generated, signed/opaque token — **never** the raw MAUI `deviceCode` itself, to prevent an attacker who learns a victim's device code from hijacking their OIDC redirect into the attacker's own pairing session.
  - `GET /api/v1/auth/external/{provider}/callback` — `[AllowAnonymous]`, reads `sub`/`oid`/`email` claims, looks up an existing `OperatorExternalIdentity` or checks for an existing `Operator` with a matching email (surface this match to the approval UI in phase 2 rather than silently creating a duplicate operator) before creating-or-linking via a shared helper — extract this helper now, phase 4's LDAP path reuses it. On success: unapproved/unlinked operator → redirect to a "pending approval" page; approved operator → mint an opaque session token via `ISessionTokenService` exactly like password login does, then either hand it to the browser the same way existing login responses do, or — when the correlation token maps to a device-code session — call `DeviceCodePairingService.TryApprove` so MAUI's existing device-code poll picks it up (confirmed `DeviceCodePairingService.cs` already exists in `VideoForensics.Hosting` and implements this exact "headless client gets a token from a browser flow" pattern for the MCP bridge; reuse it rather than building a second pairing primitive).
  - All three routes get `.RequireRateLimiting("auth")`, matching `OperatorAuthEndpoints.cs`.
  - Reachability guard: external login endpoints only function once `IOperatorRepository.IsEmptyAsync()` is false (first-run `/setup` must complete first) — mirrors `AuthGate.razor`'s existing redirect logic; don't let external auth become a bootstrap bypass.

**Client (MAUI):** a thin page that opens the system browser at the challenge URL with a `deviceCode`, then polls the same endpoint the MCP bridge's pairing flow already polls.

**UI:** `AuthProviders.razor` (admin config, from phase 1) stays `SuperAdminLocal`-gated. The actual login page consumes the new anonymous `/api/v1/auth-providers/public` endpoint to render "Sign in with…" buttons.

**Tests:** callback-handler tests with a mocked claims principal (new identity → unapproved operator created; existing approved identity → token minted; existing unapproved identity → redirected to pending; matching-email existing operator → surfaced for linking, not auto-duplicated; device-code path calls `TryApprove` not a direct browser token; disabled-provider re-check blocks even a still-registered scheme), `ExternalOidcSchemeRegistrar` tests (only enabled providers get schemes; a bad config for one provider doesn't prevent others from registering or crash startup), public-endpoint test (never leaks `ClientId`/`SecretRef`).

**NuGet:** `Microsoft.AspNetCore.Authentication.OpenIdConnect` (covers both MSA and Google — no `Microsoft.Identity.Web` needed since that package's value-add is Azure AD/multi-tenant handling, which is out of scope) — check for version conflicts against the existing `Fido2`/`Fido2.AspNet` (4.1.0) dependencies before locking versions. No new `.csproj` created, so no `Microsoft.CodeAnalysis` action needed.

---

## Phase 4 — LDAP bind for on-prem AD app login

**Server:**
- `src/client/web/VideoForensics.WebApp/Services/ILdapAuthenticationService.cs` + `LdapAuthenticationService.cs` — binds via `System.DirectoryServices.Protocols.LdapConnection` against `AuthProviderConfig.LdapServerUrl`/`LdapBaseDn`. **Must enforce LDAPS/StartTLS** — add a `UseSsl` field to `AuthProviderConfig` and refuse to bind (fail closed, log an error) if the connection isn't encrypted; binding with a password over plaintext LDAP (port 389) leaks credentials on the wire, and this app already treats "no plaintext passwords" as a hard rule. **Note:** `VideoForensics.WebApp.csproj` targets plain `net10.0`, not `net10.0-windows`, so this Windows-only API needs either a `<SupportedOSPlatform>windows</SupportedOSPlatform>` annotation + `OperatingSystem.IsWindows()` guards, or the equivalent CA1416 suppression pattern already used elsewhere in the repo for platform-specific calls (check `NetworkTierConfigReader.cs`/storage-location code for the existing pattern before inventing a new one).
- `OperatorAuthEndpoints.cs` addition: `POST /api/v1/auth/login/ldap` — on successful bind, calls the same create-or-link-then-check-approved helper extracted in phase 3.

**UI:** LDAP username/password form on the login page, shown only when `OnPremAd` is `IsEnabledForLogin`.

**Tests:** `LdapAuthenticationServiceTests.cs` against a mocked LDAP connection interface — no real domain controller in unit tests (a live-DC integration test project is explicitly out of scope for this phase). Endpoint tests reusing the phase-3 shared helper's existing test coverage.

**NuGet:** `System.DirectoryServices.Protocols` (framework-provided on Windows; confirm no version pin conflicts).

---

## Phase 5 — SMB share group-membership admin screen

**Server:**
- `src/client/web/VideoForensics.WebApp/Services/IWindowsGroupMembershipService.cs` + `WindowsGroupMembershipService.cs` — shells out to `net.exe localgroup <group> <domain\user> /add|/delete` (matches the installer's own approach, avoids a new AD SDK dependency), `ListMembersAsync(group)` (parses `net localgroup <group>` output), `IsDomainJoinedAsync()` wrapping `System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain()` in try/catch. No such generic process-runner abstraction exists in the repo today (confirmed — only ffmpeg-specific `ProcessStartInfo` usage) — introduce a small `IProcessRunner` seam here so the service is unit-testable without actually shelling out.
- `SmbGroupMembershipDtos.cs`: `SmbGroupMemberDto`, `AddSmbGroupMemberRequest`.
- `src/client/web/VideoForensics.WebApp/Api/SmbGroupMembershipEndpoints.cs` — `/api/v1/smb-groups/{group}/members` GET/POST/DELETE, `SuperAdminLocal` + step-up on mutations; returns a notice payload (not an error) when the machine isn't domain-joined or when a non-AD provider is targeted.

**Client:** `RemoteSmbGroupMembershipService.cs` in `Hosting/Remote/`.

**UI:** `src/client/ui/VideoForensics.Ui.Shared/Pages/SmbGroupMembership.razor`, route `/settings/smb-groups`. Domain-joined → AD principal add/remove form. Not domain-joined (or MSA/Google selected) → notice: "SMB group membership can only be managed automatically for on-prem Active Directory. For other providers, add or remove Windows accounts manually via Computer Management > Local Users and Groups, or `net.exe localgroup`." **Preserves the installer's deliberate "opt-in every time, never sticky" posture** (`Flags: unchecked`, no `checkedonce` in `VideoForensics.iss`) per the threat-model note — this page manages membership for shares that are already running, it must not make turning sharing itself on any more convenient or persistent than the installer already deliberately makes it, and carries the same evidence-exposure warning the installer's task description does.

**Tests:** `WindowsGroupMembershipServiceTests.cs` (via the injected `IProcessRunner` fake, no real shell-outs), endpoint tests, `Remote*` tests.

**NuGet:** none (BCL, framework-provided on Windows).

---

## Explicit deferrals (not phase 1-5, don't build now)

- **Azure AD/Entra ID entirely** — explicitly out of scope, not just deferred; the org doesn't use it. If that changes later, it slots in as a fourth `AuthProviderConfig`/`ProviderType` value following the same OIDC pattern as MSA/Google.
- **Hot-reloadable OIDC schemes** — phase 3 requires an app restart after enabling a provider; no dynamic re-registration.
- **SMB integration for MSA/Google** — permanently out of scope, not deferred (no viable path without disproportionate complexity); the notice UI is the permanent answer.
- **Encrypted-secret-file store** for client secrets — phase 1 ships env-var references only; a DPAPI-encrypted-file option is a fast-follow if admins push back on manual env-var setup.
- **Live-DC LDAP integration test project** — separate, opt-in effort.
- **Group-claim-to-role auto-mapping** from AD groups — every externally-authenticated operator starts at `ReadOnly` + unapproved; no auto-role-mapping in phases 1-5.
- **`VideoForensics.Mcp`** — confirmed it's a pure stdio↔HTTP proxy with zero data/admin access today; no auth-provider code touches it in any phase.

## Security checklist (cross-cutting — verify at implementation time, not just at design time)

- **OIDC correlation cookie** (phase 3): `AddOpenIdConnect` needs a companion cookie scheme for nonce/state/PKCE-verifier during the redirect round-trip. Must be short-lived, `HttpOnly`, `Secure`, and never reused as or confused with the app's real `PairedDevice` session mechanism.
- **PKCE explicit, defaults untouched** (phase 3): `UsePkce = true` set explicitly; do not relax `TokenValidationParameters` (audience/issuer/lifetime) on any provider to "make it work."
- **Session token never in a URL** (phase 3): applies to both the MAUI device-code path (already noted in phase 3) and the WebApp browser callback — reuse whatever delivery mechanism `OperatorAuthEndpoints`' existing password-login response already uses; never put the token in a query string (proxy/access-log/Referer leakage).
- **HTTPS required for non-loopback auth traffic** (phases 0/3/4): verify password login, LDAP login, and the OIDC callback all refuse plaintext HTTP whenever `NetworkTier != Local`.
- **Username-enumeration timing leak** (phase 0): while touching the password-login handler for lockout, make the "user not found" path perform a dummy password-hash verification rather than short-circuiting, so response timing can't reveal account existence.
- **LDAP: no second stored secret** (phase 4): bind with the operator's own submitted credentials; any follow-up directory attribute lookup (email, display name) uses that same authenticated connection — don't provision a stored LDAP service-account credential just to do lookups.
- **LDAP connection timeout** (phase 4): set an explicit short timeout on `LdapConnection` so an unreachable/slow DC can't tie up request threads.
- **MaxMind license key is a secret** (phase 0): goes through the same `SecretRef`/env-var pattern as OIDC client secrets and the LDAP bind password — not a plaintext config column, even though it doesn't look like a "credential" the way a password does.
- **Never log secrets or tokens**: no full request-body logging, no `Authorization`-header logging, on any login/callback/LDAP endpoint added in phases 0/3/4.
- **Vulnerability scan on new dependencies specifically**: run `dotnet list package --vulnerable` once `Microsoft.AspNetCore.Authentication.OpenIdConnect`, `MaxMind.GeoIP2`/`MaxMind.Db`, and `System.DirectoryServices.Protocols` land — beyond CLAUDE.md's normal outdated-package check in the full gate, since these are higher-value targets than a typical dependency.

## Critical files (existing, for reference/reuse)

- [Operator.cs](src/data/common/data.common/Entities/Operator.cs) / [OperatorRole.cs](src/data/common/data.common/Entities/OperatorRole.cs)
- [OperatorAuthEndpoints.cs](src/client/web/VideoForensics.WebApp/Api/OperatorAuthEndpoints.cs)
- [DeviceManagementEndpoints.cs](src/client/web/VideoForensics.WebApp/Api/DeviceManagementEndpoints.cs)
- [AuthorizationPolicies.cs](src/client/web/VideoForensics.WebApp/Auth/AuthorizationPolicies.cs)
- [PairedDeviceAuthenticationHandler.cs](src/client/web/VideoForensics.WebApp/Auth/PairedDeviceAuthenticationHandler.cs)
- [DeviceCodePairingService.cs](src/client/host/VideoForensics.Hosting/DeviceCodePairingService.cs)
- [VideoForensicsHostingExtensions.cs](src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs)
- [AccountDtos.cs](src/core/api/VideoForensics.Api.Contracts/AccountDtos.cs) (DTO/mapping convention reference)
- [SecurityOperators.razor](src/client/ui/VideoForensics.Ui.Shared/Pages/SecurityOperators.razor)
- [VideoForensics.iss](deploy/windows/VideoForensics.iss) (`CreateNetworkShares`, `TODO(user-management)`)

## Execution & verification

- Per CLAUDE.md: each phase's implementation work is dispatched to Haiku subagents file-by-file/service-by-service, TDD-style (test first, confirm it fails, implement, confirm it passes) — the main session does not write implementation code directly.
- After each phase (and after each file/service batch within a phase), run the lite gate: incremental `dotnet build` on touched `.csproj` files, `dotnet test` on their sibling `tests/` projects, fix failures before continuing.
- Phase 1 and 2 are independently verifiable via `dotnet test` + manually exercising the `/settings/auth-providers` page and pending-approval grid in the browser (no live IdP needed).
- Phase 3/4 need a real Microsoft (MSA) app registration / Google OAuth client / test domain controller to verify end-to-end — flag this to the user before starting those phases, since it requires credentials/config only they can provide. **Operational prerequisite, not code**: an MSA-capable app registration still requires a Microsoft Entra tenant (free tier is sufficient) even though Azure AD itself is out of scope, and Google OAuth requires a Google Cloud project — both are one-time setup the user needs to do before phase 3 can be verified end-to-end. If Google's OAuth consent screen stays in "testing" mode (fine for a small, known set of operators), confirm the expected operator count stays under Google's ~100-test-user cap or plan for consent-screen verification.
- The full gate (clean rebuild, solution-wide package bump, full test suite) runs once, before opening a PR — ask the user for confirmation first, per CLAUDE.md.
