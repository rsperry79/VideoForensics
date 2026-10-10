# Deferred and Open Items

Consolidated from the archived plans in `plans/archive/`. Abandoned items are excluded. Status is checked against `origin/dev` (2026-10-10, head `209afdb7`). Nothing here is scheduled.

Note: `plans/archive/` is gitignored and is not present in every checkout, so `archive/` source links can only be checked on disk where that folder exists.

Each item links to its plan in `plans/`, written 2026-10-09 by a Sonnet planning pass against `origin/dev`. Sections are ordered by priority, highest first. Priority weighs product value to the forensic evidence workflow, security exposure, and effort (S / M / L).

## Before starting any item

Check each item against the **dev** repo before doing any work. Archived status can be stale, and the status below can be stale too.

1. `git fetch` and confirm the item's current state on `dev` (`git log dev --oneline`, grep the code it names).
2. Check whether it already landed, was superseded, or was reversed since the source plan was written.
3. If it is already done, mark it done here with the commit or PR and skip it.
4. If it is still open, promote it to its own plan and branch off `dev` per CLAUDE.md.

## When an item is completed

Update this doc in the same PR that completes the item. Remove the item from its open section, or move it to a short completed list with the PR or commit that finished it. Also update the date in the status line. Do not leave completed items in the open sections.

## Not started

- [ ] **Evidence store backup and disaster recovery.** Priority: **High**, effort L. A manual metadata-only JSON export/import exists (commit 02fc4f6, `BackupEndpoints`, `ImportExport.razor`). It has no media bytes, scheduling, retention, off-box copy or restore verification, and the server is now the sole evidence copy. Plan: [evidence-store-backup-dr.md](evidence-store-backup-dr.md). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 458). Verified 2026-10-10: still open. Export is JSON-only (`BackupExportOrchestrator`); no scheduled job, retention, off-box copy, or restore verification.
- [ ] **M7 — Offline sync engine.** Priority: **Low-Medium**, effort L. Nothing on `dev` implements a sync engine, local cache, or conflict policy, and MAUI is online-only through its `Remote*` classes. Encrypted client-side copies of evidence add security exposure. The cache must sit behind a new Client.Common contract, since MAUI no longer references the data layer. Plan: [m7-offline-sync-engine.md](m7-offline-sync-engine.md). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (lines 23, 187). Verified 2026-10-10: still open. No sync engine, cache, or conflict contract in `src/client/common`.
- [ ] **M8 — Multi-server sync and secure network storage.** Priority: **Low**, effort L (blocked on M7 for the master/replica half). Only the M5 seam exists (`IMediaStorageProvider`, `LocalDiskMediaStorageProvider`). The Ring download path still writes to disk directly, so a network provider does nothing for downloads until that is fixed. Plan: [m8-multi-server-sync-secure-storage.md](m8-multi-server-sync-secure-storage.md). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (lines 24, 177-191, 385). Verified 2026-10-10: still open. Only `LocalDiskMediaStorageProvider` is registered; `src/providers/ring/video/VideoDownloader.cs:50` and `src/providers/ring/core/Session.cs:900` still write to disk directly.

## Open, needs device or environment to verify

- [ ] **Simple mode browser check.** Priority: **Medium**, effort S. Never verified in a browser. No code needed: a 13-step manual script against the `webapp` config in `.claude/launch.json`. Step 7 will still show the Admin unauthorized alert until commit 67d7bf6 lands. Plan: [simple-mode-browser-check.md](simple-mode-browser-check.md). Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 13).
- [ ] **End-to-end Web Push and MAUI toast delivery.** Priority: **Medium**, effort S for tests and an injectable push-sender seam (M including the live browser and device passes). The push and toast code exists, but the send path has no tests and nothing has been sent against a live server. Plan: [e2e-web-push-and-toast-delivery.md](e2e-web-push-and-toast-delivery.md).
- [ ] **MAUI native WebAuthn end-to-end test.** Priority: **Low**, effort M (S if the native code is deleted). `WebAuthnNative.cs` and `WebAuthnCeremonyClient.cs` have no callers, no tests and no MAUI test project, and have never run against a real Windows Hello prompt. First decision: delete the native code or wire it in. The WebView passkey path already works. Plan: [maui-webauthn-native-e2e.md](maui-webauthn-native-e2e.md). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 21).
- [ ] **MAUI local app-lock verification.** Priority: **Low**, effort S. Changed: the `ILocalAuthGate` / Windows Hello gate is built (`FingerprintLocalAuthGate`, `AppLockPage`). Only a manual check on a real device remains. Drop "local unlock satisfies step-up re-auth": the server cannot trust a client-side boolean. Plan: [maui-local-app-lock-verification.md](maui-local-app-lock-verification.md). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 19).
- [ ] **Web Push over HTTPS.** Priority: **Low**, effort S (L if installer certificates are chosen). Deployment decision. Kestrel binds HTTP only and the installer provisions no certificate, so LAN browsers report push as unsupported. Recommendation: accept no push on plain-HTTP LAN, add a specific "needs HTTPS or localhost" message, and document a reverse proxy with a real certificate. Plan: [web-push-over-https.md](web-push-over-https.md).
- [ ] **LDAP live domain controller integration test.** Priority: **Low**, effort M. Out of scope for the phase and not built. Needs a reachable domain controller to run. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 189).

## Open, code needed

- [ ] **Live view telemetry over SignalR (plan phase 3c).** Priority: **Low-Medium** (Medium if MAUI live view is a near-term goal), effort L. `RemoteLiveViewSessionService`, the live-view endpoints, DTOs and telemetry hub push are absent on `dev` (verified: no `RemoteLiveViewSessionService` in `origin/dev` `src/`), and MAUI does not register `ILiveViewSessionService`. Video is out of scope, but camera-control routes need careful authorization. The client observable design landed on `dev` with #201; its plan is archived at `plans/archive/signalr-observable-client.md`. Plan: [live-view-telemetry-signalr.md](live-view-telemetry-signalr.md). Source: [on-demand-live-view.md](archive/on-demand-live-view.md) (lines 257, 259).
- [ ] **Live view browser video (WebRTC).** Priority: **Low**, effort L. Session state and telemetry exist; browser-side SDP/ICE relay is a larger follow-up. Confirm scope before Phase 8. Source: [on-demand-live-view.md](archive/on-demand-live-view.md) (line 259).
- [ ] **Fail2ban-style auto-ban.** Priority: **Low**, effort M. Layers 1-4 are live (account lockout, GeoIP, threat-intel, manual `BannedIpRange`), but nothing correlates lockouts across accounts or writes bans automatically. Design: in-memory counters, persisted ban via `BannedIpRange`, keyed on `INetworkTierResolver.ResolveClientIp`, never `X-Forwarded-For`. Plan: [fail2ban-style-auto-ban.md](fail2ban-style-auto-ban.md). Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 67).
- [ ] **Self-service password reset by email.** Priority: **Low**, effort M. SMTP sending exists (`EmailNotificationProvider`, MailKit), but there is no generic `IEmailSender`, reset-token store or reset endpoint. The localhost `RecoverPasswordAsync` has no `MapPost`, so it is unrouted on `dev`. Recommendation: make it opt-in, since LAN forensic installs often have no mail server, and an anonymous reset endpoint adds attack surface. Plan: [self-service-password-reset-email.md](self-service-password-reset-email.md). Source: [per-user-login-password-passkey.md](archive/per-user-login-password-passkey.md) (lines 236-240, 363); [per-user-login-integration-test-results.md](archive/per-user-login-integration-test-results.md) (line 212).
- [ ] **MCP per-user and per-case data scoping.** Priority: **Low**, conditional on a product decision. Effort L (M if scoped to MCP only). Needed only if multi-location or multi-victim isolation is required. MCP-only scoping would be false assurance, since the REST API and UI would still show everything. Cases already hold a device set (`CaseDevice`), which per-case scoping could build on. Plan: [mcp-per-user-and-per-case-scoping.md](mcp-per-user-and-per-case-scoping.md). Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 26).
- [ ] **MAUI local-process log source (M6).** Priority: **Low**, effort S. Optional follow-up for the log viewer. The server viewer shipped, but MAUI has no log source of its own and only writes a file log, which already covers diagnostics. Plan: [maui-local-process-log-source.md](maui-local-process-log-source.md). Source: [webapp-log-viewer.md](archive/webapp-log-viewer.md) (lines 30, 131).
- [ ] **`CreateNetworkShares` user-management TODO.** Priority: **Low**, effort S (L for an in-app SuperAdmin screen). `TODO(user-management)` is still at `deploy/windows/VideoForensics.iss` line 486. Nothing manages membership of the two share groups. Sharing is opt-in and a manual workaround is documented. Plan: [create-network-shares-todo.md](create-network-shares-todo.md). Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 246).
- [ ] **Operator details for Admin.** Effort S. Not on `origin/dev`: the fix is commit `67d7bf6` on `feature/operator-details-admin`. Admin viewers should skip the SuperAdmin-only operator fetch. Plan: [operator-details-admin-alert.md](operator-details-admin-alert.md).

## Verified done on `origin/dev` (2026-10-09)

Kept here so they are not lost. Remove once the list is re-consolidated.

- **Mobile layout optimization.** Done in commit `90c31b98` on `wip` (localizes the `Routes.razor` NotFound string; `ResponsiveLayout` was already on `dev`). Cherry-picked from `feature/mobile-layout-cleanup` (`39772a73`) and pushed to `origin/wip`. Not yet in `dev`; PR to `dev` pending. Verified 2026-10-10.
- **Console app archive.** Done on `feature/console-app-archive` (`06691dbe`, pushed, based on `wip`): the project moved to `archive/VideoForensics`, was removed from `VideoForensics.sln` and `VideoForensics.CI.slnf`, and references were updated. Not yet merged. Follow-ups: the archived project does not build (67 errors from old relative `ProjectReference` paths), and a stale tracked solution copy under `.claude/worktrees/phase-4-logger-viewer-d94d39/` still lists it. Verified 2026-10-10.

- **Update-check feature.** Landed: `UpdateCheckService` and `NotifyOnly` in `origin/dev` `src/client/host/` (`UpdateCheckServiceTests.cs` present).
- **Installer event-log source registration.** Landed: `deploy/windows/VideoForensics.iss` line 809 registers the Event Log source, and `deploy/install-service.ps1` creates it.
- **Cleanup: stale Radzen comments.** Verified: no `radzen` references in `origin/dev` `src/`. Only planning docs under `plans/` still mention it.
- **Client-side SignalR (IObservable).** Landed: PR #201 (squash `3406407c`) on `origin/dev`. Includes `IRealtimeHub` and `IRealtimeStore`, the admin `SelfTestStatus` stream, the download snapshot, and the observable download and self-test sources.
- **Web Push All-audience routing and admin push limits.** Landed on `origin/dev` in `76e2b1f3` (PR #202): `LogAndReturn` removed, All-audience pushes go to every subscription, and admin pushes require Admin+, active, and approved.
- **Simple mode: MAUI remote-client gaps.** Done. Jamming endpoints, DTOs, `RemoteJammingRepository` and DI landed in #184 (`39b51aa`). Auto-detect uses the ranged `GetHistoryAsync` overload (`JammingToolsOrchestrator`), so the unranged overload's `NotSupportedException` is not reached on client paths. Stale comment removed. Verified 2026-10-10.

## Decided out of scope (reference only, no action)

- Finding #3, bearer token over plaintext HTTP on the LAN. Skipped on user confirmation. Source: [security-perf-fixes.md](archive/security-perf-fixes.md) (lines 9-15).
- Keychain / libsecret credential storage on non-Windows. Source: [security-perf-fixes.md](archive/security-perf-fixes.md) (line 47).
- Azure AD / Entra ID. Out of scope; the org does not use it. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 214).
- SMB integration for MSA / Google. Permanently out of scope. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 216).

## Excluded

- Abandoned: frontends milestones 9-10 ([frontends-mcp-api-only-architecture.md](archive/frontends-mcp-api-only-architecture.md) line 130).
- Stale, already resolved: "Streaming chat remains deferred" ([simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) line 16). Shipped in #189.
