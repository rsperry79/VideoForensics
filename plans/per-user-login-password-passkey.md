# Move website human login to per-user (password + passkey); keep device pairing for services

## Context

Today there is exactly one way for a human to sign into the website: the QR-code/WebAuthn "pairing"
ceremony (`Pair.razor` → `PairingEndpoints.cs` register/options+complete). It is **device-bound, not
user-bound**: every completed registration unconditionally creates a **brand-new `Operator`** row
(`PairingEndpoints.cs:205-212`) and a `PairedDevice` row holding the WebAuthn credential — `Operator`
itself has no `Role` and no credential of its own; both live 1:1 on `PairedDevice`. Sign-in
(`WebAuthnClient.SignInAsync`) is a fully discoverable/"usernameless" WebAuthn assertion scoped
across ALL active `PairedDevice` rows — there is no username step anywhere, and no password
mechanism exists at all (confirmed zero password/hashing infrastructure anywhere touching
`Operator`).

This conflates two different concerns that should be separated:
- **Human web login** should be per-*user* (one `Operator` identity, usable from many browsers/devices,
  authenticated by username+password and/or a passkey registered to that person).
- **Device/service pairing** (MAUI app instances, the MCP stdio bridge, other headless/automated
  clients) should stay exactly as it is today — `PairedDevice` + WebAuthn pairing ceremony, plus the
  already-existing `DeviceCodePairingEndpoints.cs` device-code+API-key flow, both of which already fit
  this "service credential, SuperAdmin+Local approves, explicit role" shape well. **Nothing in this
  plan changes those two flows or their endpoints/entities.**

Additional requirements folded in from the conversation:
- A SuperAdmin must be able to reset another operator's password (works regardless of whether that
  operator normally signs in with a password or a passkey), or set a temporary password that the
  operator is forced to change on next login.
- New passkeys registered to an operator (beyond the very first credential that proves who they are)
  must be admin-approved before they can be used to sign in — mirrors the existing
  self-service-operator-approval precedent (`Operator.IsApproved`), just applied per-credential.
- Bootstrap (the very first operator ever) now goes through username+password setup instead of the
  passkey pairing ceremony, immediately as an approved SuperAdmin (no operators exist yet to approve
  anything).
- Self-service registration is kept: anyone can submit a username+password+display name; the account
  is created but `IsApproved=false` and role `ReadOnly` until a SuperAdmin approves it — same
  semantics as today's anonymous QR pairing self-service, just via a password form.
- Password hashing uses `Microsoft.Extensions.Identity.Core`'s `PasswordHasher<T>` (PBKDF2,
  Microsoft-maintained) — not the full ASP.NET Identity/EF store system, just this one class.

**Bug found during investigation, fixed as part of this work:** `OperatorDetails.razor`'s existing
"Approve" button (for `Operator.IsApproved`) already POSTs to `api/devices-management/operators/{id}/approve`
— but that endpoint doesn't exist anywhere in `DeviceManagementEndpoints.cs` today. It 404s. This plan
adds it while building the near-identical credential-approval endpoint.

## Data model changes

File: `src\data\common\data.common\Entities\Operator.cs`
- Add `string Username` (required, unique — the login handle; distinct from the existing
  `DisplayName`).
- Add `OperatorRole Role` — **becomes canonical**. (Today role lives only on `PairedDevice`, which is
  in tension with `OperatorRole`'s own doc comment: "a role travels with the person across every
  device." Migration copies each operator's existing single `PairedDevice.Role` into this new column;
  `PairedDevice.Role` and its existing service/device semantics are otherwise untouched — a service
  credential's role stays independently settable at device-code approval time, since a service may
  legitimately need a narrower role than its approving admin.)
- Add `string? PasswordHash` (PBKDF2 hash via `PasswordHasher<Operator>`, null until a password is
  set).
- Add `bool MustChangePassword` (default false) — set true whenever a SuperAdmin issues a temp
  password.
- Add `DateTime? PasswordUpdatedAtUtc`.
- Add `string FirstName`, `string LastName` (required — contact identity, distinct from the existing
  freeform `DisplayName` which stays as-is and is unaffected).
- Add `string Email` (required, unique — DB unique index alongside `Username`'s).
- Add `string? Phone` (optional).
- Add `Guid SecurityStamp` (regenerated on creation and on every password reset/change — see the
  session-invalidation gap below).
- Add `DateTime? ApprovalFirstLoginNotifiedAtUtc` (the one-shot "newly-approved account's first login"
  notification flag — see admin-notifications gap below).
These four are collected on the registration/setup form (bootstrap and self-service alike) and
editable afterward from `OperatorDetails.razor`, following the same pattern as the existing
`SaveDisplayName` action (`OperatorDetails.razor:266-291`) — not step-up gated, since editing your own
contact info is low-risk, same tier as the display name. `Email` is a natural fit with the existing
outbound-email infrastructure (`EmailNotificationProvider`/`SmtpPasswordStore`, already used for
notifications) — worth revisiting later whether a reset temp password should also be emailed to the
operator in addition to being shown once to the admin, but that's an enhancement, not required for
this plan: the primary flow (admin sees it once, copies it, hands it to the operator out-of-band)
doesn't depend on SMTP being configured on every install.

New entity: `src\data\common\data.common\Entities\OperatorCredential.cs` — a passkey bound to an
*operator*, not a device. Fields: `Id, OperatorId, string Label` (e.g. "Chrome on Richard's PC", user
-supplied at registration), `string WebAuthnCredentialId`, `byte[] WebAuthnPublicKey`,
`uint WebAuthnSignCount`, `DateTime CreatedAtUtc`, `DateTime? LastUsedAtUtc`,
`bool IsApproved` (false until a SuperAdmin/Admin approves it — except see below),
`DateTime? RevokedAtUtc`, `string? RevokedReason`, computed `IsActive => RevokedAtUtc == null`,
`DateTime? FirstLoginNotifiedAtUtc` (one-shot "newly-approved credential's first login" flag).
This is intentionally a **separate table from `PairedDevice`**, not a repurposing of it — keeps the
service/device model (which several other flows already depend on) completely unmodified, and avoids
overloading `PairedDevice`'s existing "exactly one of WebAuthn-pair or FallbackApiKeyHash" contract
with a third, differently-scoped credential kind.

**Approval rule for `OperatorCredential.IsApproved`:** the credential created during initial
registration (self-service signup, or the one a SuperAdmin creates while provisioning a new operator)
is approved automatically **together with** the operator itself — there is nothing to separately
approve at that point, since approving the operator already vets that first credential. Only
credentials added *later* to an already-approved operator (self-service "add another passkey," e.g.
from a new browser) start `IsApproved=false` and need a separate admin approval before they can be
used to sign in.

Migration: one new EF Core migration adding all of the above (`Operators` columns +
`OperatorCredentials` table), following the existing migration patterns under
`src\data\database\sqlite\data.database.sqlite\Migrations\`.

Repositories:
- `IOperatorRepository` (`src\data\common\data.common\Contracts\IOperatorRepository.cs`): add
  `GetByUsernameAsync(string username, CancellationToken ct)`, `SetPasswordAsync(operatorId, passwordHash, mustChangePassword, CancellationToken ct)`, `SetRoleAsync(operatorId, role, CancellationToken ct)`. Fix/confirm `ApproveAsync` already exists (it does) — just needs its endpoint wired (see below).
- New `IOperatorCredentialRepository` + `OperatorCredentialRepository` (mirror
  `IPairedDeviceRepository`/`PairedDeviceRepository`'s shape closely — `GetByWebAuthnCredentialIdAsync`,
  `ListForOperatorAsync`, `ListPendingApprovalAsync`, `AddAsync`, `ApproveAsync`, `RevokeAsync`,
  `RecordSuccessfulAuthAsync`).

## Session/auth plumbing changes

`SessionPrincipal` (`src\client\host\VideoForensics.Hosting\SessionTokenService.cs`): generalize the
device-specific field. Replace the implicit "this token always maps to exactly one `PairedDevice`"
assumption with an explicit `CredentialKind { ServiceDevice, OperatorPasskey, Password }` plus a
nullable `CredentialId` (the `PairedDevice.Id` for `ServiceDevice`, the `OperatorCredential.Id` for
`OperatorPasskey`, or `null` for a plain password session). `Role` now always comes from
`Operator.Role` at issuance for the two new kinds (still from `PairedDevice.Role` for `ServiceDevice`,
unchanged).

`PairedDeviceAuthenticationHandler` (`src\client\web\VideoForensics.WebApp\Auth\PairedDeviceAuthenticationHandler.cs`):
branch the existing per-request re-validation (lines ~61-93) on `CredentialKind` — `ServiceDevice`
keeps today's exact `PairedDevice`/`Operator` checks; the two new kinds check `Operator.Active`/
`IsApproved` and, for `OperatorPasskey`, that the specific `OperatorCredential` is still
`IsActive`/`IsApproved`. The fallback API-key path (service devices only) is untouched.

## New server endpoints

All in `VideoForensics.WebApp`, versioned `/api/v1/...` (learned the hard way earlier this session —
double-check every new route against what the client actually calls).

- `POST /api/v1/auth/register` — username+password+display name self-service signup. Creates
  `Operator` (`IsApproved = !IsEmptyAsync` i.e. bootstrap becomes SuperAdmin+approved automatically,
  everyone else `ReadOnly`+unapproved) and its first `OperatorCredential`-free password identity
  (no passkey required at signup).
- `POST /api/v1/auth/login/password` — validates username+password (`PasswordHasher<Operator>.VerifyHashedPassword`).
  On success: if `MustChangePassword`, issue a session flagged accordingly (client redirects straight
  to a forced change-password screen — see UI); otherwise issue a normal session via
  `SessionTokenService.Issue(..., CredentialKind.Password, credentialId: null, operator.Role)`.
- `POST /api/v1/auth/change-password` — authenticated; requires current password unless the session
  is flagged `MustChangePassword` (temp-password flow); sets new hash, clears the flag.
- `POST /api/v1/auth/webauthn/username-assertion-options` and `.../username-assertion-complete` —
  the username-first passkey ceremony: options are scoped to `AllowedCredentials` built from that one
  operator's *approved* `OperatorCredential` rows only (today's existing
  `/api/v1/auth/webauthn/assertion-options`/`assertion-complete` stay completely unchanged and keep
  serving the discoverable, all-`PairedDevice` flow used by service clients).
- `POST /api/v1/auth/operator-credentials/register/options` and `.../register/complete` —
  authenticated (via password or an existing approved passkey); registers a NEW `OperatorCredential`
  for the current operator (`IsApproved=false`, per the rule above).
- `GET /api/v1/devices-management/operator-credentials/mine` — self-service list of the current
  operator's own credentials (for a "My Passkeys" page).
- `GET /api/v1/devices-management/operator-credentials/pending` — admin view of all
  `IsApproved=false` credentials across every operator.
- `POST /api/v1/devices-management/operator-credentials/{id}/approve` and `.../revoke` —
  Admin+, step-up gated (same `X-StepUp-Token` pattern already used for `PairedDevice` revoke and
  intended for `Operator` approve).
- `POST /api/v1/devices-management/operators/{id}/approve` — **the missing endpoint** the UI already
  calls; add it now (`IOperatorRepository.ApproveAsync` already exists, just needs wiring), Admin+,
  step-up gated, matching the pattern of the adjacent `deactivate` endpoint in the same file.
- `POST /api/v1/devices-management/operators/{id}/reset-password` — SuperAdmin+, step-up gated.
  Generates a random temporary password, hashes and stores it, sets `MustChangePassword=true`,
  returns the **plaintext temp password in the response body only** (never persisted, never logged) —
  the admin UI shows it once with a copy button and a "won't be shown again" warning.

## Gaps closed on second pass

- **Step-up re-auth breaks for the new credential kinds.** `WebAuthnClient.StepUpAsync` +
  `/api/v1/auth/webauthn/stepup-complete` only know how to re-assert against the current session's
  `PairedDeviceId` claim — a password-only operator has no passkey to step up with at all, which would
  make every step-up-gated action (Approve, Revoke, Reset Password) unreachable for them. Add a
  parallel `POST /api/v1/auth/stepup/password` (re-verify the current password, issue the same
  short-lived step-up token) for `CredentialKind.Password` sessions; `CredentialKind.OperatorPasskey`
  sessions reuse the existing passkey step-up path, just resolved via `OperatorCredentialId` instead of
  `PairedDeviceId`. The client-side step-up call picks the right path based on how the current session
  was established.
- **Rate limiting.** Every new auth-adjacent endpoint (`login/password`, `register`, `change-password`,
  the username-assertion pair, `stepup/password`) gets the same `.RequireRateLimiting("auth")` already
  applied to the existing pairing endpoints — password auth is a materially bigger brute-force target
  than passkeys, so this isn't optional.
- **Username uniqueness is a real DB constraint**, not just an app-level check — add a unique index on
  `Operators.Username` in the migration, so a race between two concurrent signups can't create
  duplicates.
- **Audit logging.** `login/password`, `register`, credential approve/revoke, and reset-password all
  call the existing `ISecurityAuditLogger` (already used by `register/complete`/`revoke`/`deactivate`)
  for consistency with the rest of the security-sensitive surface.
- **Admin notifications, two points, not one.** (1) Self-service password signups and newly-pending
  credentials dispatch a notice via `INotificationDispatcher` the same way pending self-service
  operators do today. (2) A second, distinct notice fires the *first time* a newly-approved credential
  (a freshly-approved `OperatorCredential`, or a freshly-approved self-service `Operator`'s first
  login) is actually used to sign in successfully — a closing confirmation that the approved identity
  is now genuinely active, not just theoretically trusted. Track this with a one-shot flag
  (`OperatorCredential.FirstLoginNotifiedAtUtc`/an equivalent on `Operator` for the account-approval
  case) so it fires exactly once and never again for that credential/account. Per the notice-audience
  fix earlier this session, both notice types are `Audience.AdminsOnly` and will now actually surface
  to admins in the bell instead of silently piling up. Ordinary subsequent logins (from
  already-established, previously-used credentials) do NOT generate a notice — this is a one-time
  bookend around new-identity trust, not a full login audit trail.
- **Login response is explicit about `mustChangePassword`.** The `login/password` response body
  includes `mustChangePassword: bool` directly (not inferred from a separate call), so
  `PairedSessionState`/the sign-in page can redirect immediately without a round trip.
- **Existing operators have no password after this ships.** Pre-existing `Operator` rows get a
  generated (deduplicated) `Username` from `DisplayName` during migration, but `PasswordHash` stays
  null — they can't log into the website with a password until a SuperAdmin resets one for them via
  the new reset-password flow. Call this out explicitly rather than let it surprise anyone; it's a
  one-time transition cost, not a bug.
- **Password reset must kill existing sessions, not just future ones.** `SessionTokenService` validates
  a session purely from its own encrypted content plus a live `Operator.Active`/`IsApproved` DB check —
  it never re-checks the password. Today, if a SuperAdmin resets a compromised operator's password
  (the exact incident-response scenario this feature exists for), that operator's already-issued
  session token(s) keep working until their 12h TTL naturally expires. Fix: bake `Operator.SecurityStamp`
  into `SessionPrincipal` at issuance; `PairedDeviceAuthenticationHandler`'s per-request re-validation
  (for `Password`/`OperatorPasskey` session kinds) also compares the stamp and rejects on mismatch.
  Regenerate the stamp on every password reset/change — this immediately invalidates every existing
  session for that operator, forcing a fresh login (with the new/temp password) everywhere. Ordinary
  `ServiceDevice` sessions are unaffected (they already have their own live revocation check).
- **`MustChangePassword` is enforced server-side, not just by client redirect.** `AuthGate.razor`
  redirecting to `/change-password` is a UX nicety, not a security boundary — a session with
  `MustChangePassword=true` must be rejected server-side (403, distinct error code the client already
  handles) for every request except `change-password` itself and sign-out, enforced in
  `PairedDeviceAuthenticationHandler`/a small piece of endpoint middleware, so a client that ignores the
  redirect can't still use the temp-password session for anything else.
- **No cross-operator IDOR on the new self-service endpoints.** `operator-credentials/register/*`,
  `operator-credentials/mine`, and `change-password` all derive the target `OperatorId` from the
  authenticated session's own claim (`vf:operator_id`), never from a client-supplied id in the request
  body/route — otherwise one operator could register a passkey onto, or read the credential list of,
  a different operator's account. (The existing admin endpoints, e.g. approve/revoke/reset-password,
  legitimately take a target `{id}` in the route — that's fine, they're already Admin/SuperAdmin+
  step-up gated; this bullet is specifically about the *self-service* endpoints, which must not take
  an operator id as client input at all.)
- **Minimum password strength.** `register`/`change-password`/reset-password's generated temp password
  all enforce a basic minimum (length, not full complexity theater) — reuse whatever validation
  convention the codebase already has for similar user-facing input constraints, or a simple explicit
  check (e.g. 12+ characters) if none exists; not meant to be elaborate, just not absent.
- **Explicitly out of scope for this plan:** a self-service "forgot my password" email-reset flow.
  `Email` is now captured and the outbound-email infra exists, but a secure token-based self-reset
  flow is its own scoped piece of work depending on reliable SMTP being configured — the SuperAdmin
  reset-password flow this plan builds covers the requirement that was actually asked for. Worth a
  follow-up later, not bundled in here.
- **Verify before repurposing `DeviceSignIn.razor`:** MAUI has its own native WebAuthn ceremony helper
  (`VideoForensics.MauiApp\WebAuthn\WebAuthnCeremonyClient.cs`), separate from `Ui.Shared`'s
  `WebAuthnClient.cs`/`DeviceSignIn.razor`, which suggests MAUI does NOT route its own device pairing
  through this shared page — but this needs to be confirmed (grep MAUI's actual navigation/DI wiring
  for whether `DeviceSignIn.razor` is reachable/used from the MAUI head at all) at the start of Phase 4,
  before repurposing that page for human password+passkey login, to avoid silently breaking MAUI sign-in.

## UI changes

- `DeviceSignIn.razor` (or split into a new `SignIn.razor` if that reads more clearly once
  password-first) gains a Username field, Password field, "Sign In" button (→ `login/password`), and
  a secondary "Sign in with a passkey instead" path that, once a username is entered, drives the new
  username-scoped assertion ceremony via a `WebAuthnClient` addition (`SignInWithUsernameAsync(username)`).
  On a `MustChangePassword` response, redirect straight to a new `ChangePassword.razor` instead of the
  normal post-login destination.
- New `ChangePassword.razor` — current password (skipped if arriving via the forced
  `MustChangePassword` path) + new password + confirm; calls `change-password`.
- New self-service `MyPasskeys.razor` (or a section on an existing operator-profile page if one
  exists) — lists the operator's own `OperatorCredential`s (Pending/Approved/Revoked), "Add Passkey"
  button driving `operator-credentials/register/*`, step-up-gated Revoke — modeled directly on
  `SecurityDevices.razor`'s existing grid+step-up-revoke pattern.
- New admin "Pending Credentials" view (a tab/section on `SecurityOperators.razor`, or its own page
  gated `Admin+`) — lists `operator-credentials/pending`, Approve/Revoke buttons, step-up gated.
- `OperatorDetails.razor` — add a "Reset Password" button (SuperAdmin, step-up gated) calling the new
  reset-password endpoint, displaying the returned temp password once (copy-to-clipboard, explicit
  "shown once" warning, no plaintext ever stored or re-displayed).
- `Welcome.razor` — "Create New Account" now routes to the new password-based `/register` /setup flow
  instead of `/pair` (bootstrap and self-service both go through it; bootstrap is just
  `IsEmptyAsync()==true` at registration time, handled server-side as already described).
- `AuthGate.razor` — add the new sign-in/register/change-password routes to `AllowListedRoutes`; add a
  `MustChangePassword` check (surfaced via `PairedSessionState`/session claims) that forces a redirect
  to `/change-password` ahead of the normal allow-list/redirect logic, the same priority level as
  today's signed-in check.
- **Unchanged, on purpose:** `Pair.razor`, `DevicePairingApproval.razor`, `SecurityDevices.razor`,
  `DeviceCodePairingEndpoints.cs`, and the original `PairingEndpoints.cs` WebAuthn
  register/options+complete and assertion-options/assertion-complete endpoints — these remain exactly
  as they are today, now understood as the **service/device** pairing surface (MAUI, MCP bridge,
  other headless clients), not the human web login surface.

## Verification

- Unit tests for the new repositories (`OperatorCredentialRepository`, the new `IOperatorRepository`
  methods) following the existing `SqliteInMemoryFixture`/`TestDataBuilder` pattern in
  `data.database.tests`.
- Unit tests for `PasswordHasher<Operator>` usage (hash → verify round-trip, wrong-password rejection)
  wherever the login endpoint's logic is unit-testable (extract a small `IOperatorLoginService` if the
  Minimal API handler itself isn't easily testable in isolation — check how `RingAuthService`-style
  services are tested for the pattern to mirror).
- `dotnet build` + lite gate (touched projects only) after each phase; full gate before any PR per
  `CLAUDE.md`.
- Manual browser walkthrough via the dev server: fresh DB → `/welcome` → "Create New Account" →
  bootstrap SuperAdmin via username+password → sign in → add a passkey from "My Passkeys" → confirm
  it's `Pending` and can't sign in with it yet → from a second (or the same, for testing) SuperAdmin
  session, approve it in the admin Pending Credentials view → confirm passkey sign-in now works →
  test SuperAdmin "Reset Password" on that operator → confirm forced `/change-password` on next
  login. Separately confirm the existing `/pair` (QR passkey) and device-code flows still work
  unmodified for a "service" pairing.

## Execution plan (phased dispatch)

Given the size, implementation is dispatched to Haiku subagents in dependency order, not all at once:
1. Data model + migration + repositories (must land first — everything else depends on it).
2. Session/auth plumbing (`SessionPrincipal`, `PairedDeviceAuthenticationHandler`) — depends on (1).
3. Server endpoints — depends on (1) and (2).
4. UI — depends on (3)'s routes existing.
5. Tests — can start as soon as the relevant repository/endpoint from earlier phases lands, run in
   parallel with later phases where there's no file overlap.
Each phase's lite gate (build + relevant tests) runs before moving to the next phase.
