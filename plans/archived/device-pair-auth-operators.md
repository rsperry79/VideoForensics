# Plan: device-pair sign-in, operator details, login-method settings

Branch: `claude/device-pair-auth-operators-fb6bb0` (off `dev`). Status: **PAUSED, nothing implemented yet.**

## Root causes found
- `DeviceSignIn.razor` only offers username/password (+ username-scoped passkey). The usernameless device-passkey path (`WebAuthnClient.SignInAsync()` -> `/api/v1/auth/webauthn/assertion-options|complete`) exists but is unused. Paired devices get a `device-<guid>` operator with no password, so nobody can sign in.
- `Pair.razor` finishes with a link to `/device-signin` instead of signing in.
- `PairingEndpoints` `register/complete` always creates a new Operator, even when the caller is already signed in.
- `SecurityOperators.razor` shows the Details button only for Active operators; display name isn't a link.
- No setting exists to enable/disable login methods.

## Decisions (from user)
- Auth providers = login methods: Password, Passkey, plus Microsoft (MSA), Google, Facebook.
- Scope update: **Microsoft/Google/Facebook are skipped entirely** (no settings, no OAuth). Only Password and Passkey toggles ship now; external providers are a later follow-up (MSAL / ASP.NET external-login handlers).
- Pairing while signed in attaches the device to the signed-in operator; only anonymous pairing creates a new (pending) operator.

## Work items
### A. Login methods + sign-in (WebApp / Ui.Shared)
1. `IAuthMethodSettingsService` (+impl, DI) over `IAppSettingRepository`. Keys: `AuthPasswordEnabled`, `AuthPasskeyEnabled` (default true); `AuthMicrosoftEnabled`/`AuthGoogleEnabled`/`AuthFacebookEnabled` (default false) + `Auth<X>ClientId`; secrets stored encrypted (mirror SMTP password store / `ICredentialEncryptionProvider`), never returned (only `hasClientSecret`).
2. `Api/AuthMethodEndpoints.cs`: anonymous `GET /api/v1/auth/methods` (enabled flags only; external true only if enabled and configured); `GET/PUT /api/v1/auth-methods/settings` behind `SuperAdminLocal`, PUT with `StepUpEndpointFilter`, rejects disabling both Password and Passkey; audit `AuthMethodsUpdated` (+ `DefaultUrgency`). DTOs in `Api.Contracts`.
3. Enforce: 403 in `LoginPasswordAsync`, username-assertion handlers, and `assertion-options/complete` when the method is disabled. First-run setup unaffected.
4. UI: `Pages/SecurityAuthMethods.razor` (`/settings/auth-methods`, modeled on `SecurityLockoutPolicy.razor`), nav item "Login Methods" (SuperAdmin) in `NavGroups.cs` + `NavGroupsTests`.
5. `WebAuthnClient.GetAuthMethodsAsync()`; `DeviceSignIn.razor`: add usernameless "Sign in with this device's passkey" button, hide disabled methods, always land on `/` (or `/change-password`) after sign-in.

### B. Pairing + operators
1. `register/complete`: if caller is an authenticated, active, approved operator, attach the `PairedDevice` to that operator (no new Operator, role = operator's role, no pending notification); else current behavior. Verify the bearer token populates `context.User` on this route.
2. `WebAuthnClient.CompleteRegistrationAsync(..., string? sessionToken = null)`; `Pair.razor` passes the session token when signed in, then auto-signs-in via `SignInAsync()` and navigates to `/` (fallback: success alert + `/device-signin` link; unapproved keeps pending message).
3. `SecurityOperators.razor`: Details button for all operators; display name links to `/settings/operators/{id}`.

### C. DbRepair lost-passkey recovery (`src/tools/DbRepair`)
`VideoForensics.DbRepair --reset-superadmin-password [--username <n>] [--db-path|--data-root] [--yes]`: picks the primary SuperAdmin (or `--username`), prompts for a hidden new password (>=12 chars, confirmed, never via args/env), hashes with `PasswordHasher<Operator>`, sets it with `mustChangePassword:false`, re-activates/approves/unlocks the account, re-enables password sign-in if `AuthPasswordEnabled` was set false, audits if easy. Logic in a testable class + new tests project; ToolsReadme updated.

## Process (CLAUDE.md)
TDD, dispatch implementation to Haiku subagents (A and B can run in parallel; they share `WebAuthnClient.cs` and `PairingEndpoints.cs`, so use targeted edits), lite gate (build touched csproj + scoped tests), commit/push branch; full gate only before PR to `dev` (ask first).
