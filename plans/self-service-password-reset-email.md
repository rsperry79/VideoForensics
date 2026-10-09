# Self-service password reset by email

**Status on origin/dev (checked 2026-10-09):** STILL OPEN (with a caveat: the existing localhost recovery handler is not routed). SMTP sending infra exists; reset-by-email does not.

**Source:** `plans/archive/per-user-login-password-passkey.md` lines 236-240 (explicitly out of scope, "depends on reliable SMTP being configured") and line 363; `plans/archive/per-user-login-integration-test-results.md` line 212 ("requires token-based secure reset logic").

## Verdict

STILL OPEN. Grepped origin/dev for `Smtp`, `IEmailSender`, `MailKit`, `SendGrid`, `MimeKit`, `RecoverPassword`, `PasswordReset`, `forgot`. Found: `EmailNotificationProvider` (MailKit, `src/client/host/VideoForensics.Hosting/`), `ISmtpPasswordStore`/`SmtpPasswordStore`, `IForensicsConfiguration.Smtp*` settings, `Operator.Email` (required, unique), `SecurityAuditEventTypes.PasswordReset`. Not found: any generic `IEmailSender`, any reset-token store, any forgot/reset endpoint or page. Caveat: `RecoverPasswordAsync` (OperatorAuthEndpoints.cs:926) has no `MapPost` on dev, so even the localhost temp-password flow is only reachable by tests via reflection; `src/utils/repair/SuperAdminRecovery.cs` is the real offline recovery path. The existing sender is hard-wired to send to `NotificationRecipientEmail` (the owner), not to an arbitrary operator, so it must be generalized.

## Scope

- Anonymous "Forgot password" request (username or email) and "Reset password" completion with an emailed one-time token.
- Reuse the existing SMTP configuration; gate the whole feature on SMTP being enabled and tested.
- Non-enumerating: request endpoint always returns the same 202/generic message and takes comparable time, whether or not the account exists, has an email, or SMTP fails.
- Token: 256-bit CSPRNG, only a SHA-256 hash stored, 30-minute expiry, single-use (consumed atomically), at most one live token per operator (new request invalidates old), invalidated on any password change or SecurityStamp rotation.
- On success: set new hash (`MustChangePassword=false`), rotate `SecurityStamp`, reset lockout counters, audit `PasswordReset` (urgent), notify the operator that the password changed.
- Rate limit on both endpoints via the existing `auth` limiter plus a per-operator request cap (e.g. 3 per hour).
- Out of scope: SMS, security questions, passkey recovery, resetting the primary SuperAdmin by email (see risks).

## Open decision: how email is sent in an offline or LAN deployment

A forensic LAN install (Windows service, installer in `deploy/windows/`) often has no outbound internet and no mail relay. `deploy/windows/` has no SMTP provisioning, so nothing guarantees a mail server exists.

Recommendation: ship email reset as OPT-IN, enabled only when the existing SMTP settings are configured and a test send has succeeded (customer-supplied relay, e.g. an internal Exchange/Postfix; no bundled mail server, no vendor SaaS SDK such as SendGrid). When SMTP is not configured, the "Forgot password" link is hidden and the UI points to the existing alternatives: an Admin/SuperAdmin-issued reset (already built) and the offline `SuperAdminRecovery` tool for the primary SuperAdmin. Do not add a hosted-email dependency to a forensic product. Confirm with the owner that opt-in is acceptable before building.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/client/host/VideoForensics.Hosting/EmailNotificationProvider.cs` | Extract the MailKit send into a reusable sender; keep the notification provider delegating to it | Currently only sends to the configured owner address |
| `src/core/providers/providers-common/Contracts/IEmailSender.cs` NEW | `Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)` and `bool IsConfigured` | Public API must be an interface in Contracts/ |
| `src/client/host/VideoForensics.Hosting/SmtpEmailSender.cs` NEW | MailKit implementation of `IEmailSender` using `IForensicsConfiguration` + `ISmtpPasswordStore` | Vendor SDK stays in the service layer |
| `src/data/common/data.common/Contracts/IPasswordResetTokenRepository.cs` NEW | Create (hash, operatorId, expiry), atomic consume, invalidate-for-operator, purge-expired | Token store; needs structured table per Data Requirements |
| `src/data/common/data.common/Entities/PasswordResetToken.cs` NEW | Entity: Id, OperatorId, TokenHash, CreatedAtUtc, ExpiresAtUtc, ConsumedAtUtc | Structured schema, no JSON |
| `src/data/common/data.common/Entities/Operator.cs` | None expected (Email already exists and is unique) | Verified |
| `src/data/common/data.common/Contracts/IOperatorRepository.cs` | Add `GetByEmailAsync`; reuse `GetByUsernameAsync`, `SetPasswordAsync`, `ResetFailedLoginAttemptsAsync` | Lookup by email; SecurityStamp rotation may need a new method |
| `src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs` | Register `IEmailSender`, `IPasswordResetService`, token repo (near line 516 where `ISmtpPasswordStore` is registered) | DI wiring |
| `src/client/host/VideoForensics.Hosting/PasswordResetService.cs` NEW (+ `IPasswordResetService` in a Contracts folder) | Request/redeem logic: token generation, hashing, expiry, single use, uniform timing, audit | Keeps endpoint thin and testable |
| `src/client/web/VideoForensics.WebApp/Api/OperatorAuthEndpoints.cs` | Add `POST /api/v1/auth/password-reset/request` and `/complete` (anonymous, `auth` rate-limit policy); DTOs go in `VideoForensics.Api.Contracts` | Versioned routes + DTOs per client rules; follows existing `MapPost` pattern at lines 59-96 |
| `src/client/web/VideoForensics.WebApp/Program.cs` | Optionally add a dedicated per-operator limiter policy beside `auth` (line 181) | Cap reset emails per account |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/SignIn.razor` + `SignIn.resx` | "Forgot password?" link, shown only when email reset is available | Entry point |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/ForgotPassword.razor` + `.resx` NEW | Request form with generic confirmation text | All strings via `IStringLocalizer`, per CLAUDE.md |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/ResetPassword.razor` + `.resx` NEW | Token + new password form, min length matches `ChangePassword` | Same |
| `src/client/ui/VideoForensics.Ui.Shared/Resources/SharedResources.resx` | Shared keys (e.g. generic "If an account matches, an email was sent", "Link expired or already used", "Email reset unavailable, contact your administrator") | Localization |
| `src/client/ui/VideoForensics.Ui.Shared/Layout/AuthGate.razor` | Allow-list the two new anonymous routes | Otherwise unauthenticated visitors are redirected |
| `src/client/web/VideoForensics.WebApp.Tests/OperatorAuthEndpointsTests.cs` | New tests for the endpoints | Test project |
| `src/client/host/VideoForensics.Hosting.Tests/` NEW test files | `PasswordResetServiceTests`, `SmtpEmailSenderTests` | Test project |
| `src/client/ui/VideoForensics.Ui.Shared.Tests/` NEW test files | Page tests for ForgotPassword/ResetPassword/SignIn link | Test project |

Note: two Hosting.Tests locations exist on dev (`src/client/host/VideoForensics.Hosting.Tests/` and `src/client/host/tests/`); confirm which is live before adding files. A new data migration/schema step for `PasswordResetToken` is also needed in the data layer (path to verify at implementation time).

## Test plan

TDD: write each test first, confirm it fails (missing type/endpoint or wrong behavior), then implement. Scope local runs with `dotnet test --filter`.

- `PasswordResetServiceTests` (Hosting.Tests): `RequestAsync_UnknownUser_ReturnsSameResultAsKnownUser`, `RequestAsync_KnownUser_StoresHashedTokenNotPlaintext`, `RequestAsync_SecondRequest_InvalidatesFirstToken`, `RequestAsync_SmtpFailure_StillReturnsGenericResult`, `RequestAsync_OverPerOperatorCap_SendsNoEmail`, `CompleteAsync_ValidToken_SetsPasswordAndRotatesSecurityStamp`, `CompleteAsync_ExpiredToken_Fails`, `CompleteAsync_ReusedToken_Fails`, `CompleteAsync_WeakPassword_Fails`, `CompleteAsync_ConcurrentRedeem_OnlyOneSucceeds`, `CompleteAsync_Success_WritesUrgentPasswordResetAudit`.
- `SmtpEmailSenderTests`: `IsConfigured_SmtpHostMissing_ReturnsFalse`; send path via an abstraction over the MailKit client (Moq).
- `OperatorAuthEndpointsTests` (WebApp.Tests): `PasswordResetRequest_UnknownAndKnownUser_ReturnIdenticalResponse`, `PasswordResetRequest_SmtpNotConfigured_Returns404Or501`, `PasswordResetComplete_BadToken_Returns400WithGenericError`.
- Repository tests (data tests project): atomic consume, expiry purge.
- Ui.Shared.Tests: `SignIn_EmailResetUnavailable_HidesForgotLink`, `ForgotPassword_Submit_ShowsGenericLocalizedMessage`, resx key-presence test mirroring `SharedResourcesSimpleModeKeysTests`.
- Expected initial failure: compile errors for missing types, then assertion failures once stubs exist.

## Risks and open questions

- No mail server on many LAN installs; feature is useless there (mitigated by opt-in and hiding the link).
- Email is weaker than the current factors: anyone with the operator's mailbox can reset. Decide whether accounts with a registered passkey or 2FA still require the second factor after reset, and whether SuperAdmin/primary SuperAdmin are excluded (recommend excluding primary SuperAdmin; keep `SuperAdminRecovery`).
- Reset link host: must come from configured server URL, never the request `Host` header (host-header poisoning).
- Timing side channel: constant-ish work for known vs unknown accounts; send email on a background queue.
- Never log raw tokens, emails or operator GUIDs; log "operator" names/roles only per CLAUDE.md.
- Reset invalidates existing sessions and paired-device bearer tokens? Needs a decision.
- Cleanup of the unrouted `RecoverPasswordAsync`: confirm whether it is dead code or deliberately unmapped.
- Does the plain-text email body suffice, or do we need localized email templates?

## Priority recommendation

Low: product value is modest because an Admin/SuperAdmin reset and an offline SuperAdmin recovery tool already cover the actual requirement, a LAN forensic deployment often cannot send mail, and an anonymous reset endpoint adds real attack surface (account takeover via mailbox) for an effort of M (new token table, service, two endpoints, three pages, localization, many security tests). Revisit if customers with working SMTP ask for it.

## Implementation dispatch

Sonnet subagents, each test-first: (1) `IEmailSender` + `SmtpEmailSender` extraction from `EmailNotificationProvider`; (2) token entity, repository and migration; (3) `PasswordResetService` plus DI; (4) endpoints, DTOs, rate limiting; (5) Blazor pages, `.resx` keys and `AuthGate` allow-list; then lite-gate build and scoped tests after each batch.
