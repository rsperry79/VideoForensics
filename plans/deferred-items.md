# Deferred and Open Items (Placeholder)

Consolidated from the archived plans in `plans/archive/`. Abandoned items are excluded. This is a placeholder list, not an approved plan: nothing here is scheduled.

## Before starting any item

Check each item against the **dev** repo before doing any work. Archived status can be stale.

1. `git fetch` and confirm the item's current state on `dev` (`git log dev --oneline`, grep the code it names).
2. Check whether it already landed, was superseded, or was reversed since the source plan was written.
3. If it is already done, mark it done here with the commit or PR and skip it.
4. If it is still open, promote it to its own plan and branch off `dev` per CLAUDE.md.

## Not started

- [ ] **M7 — Offline sync engine.** MAUI local cache to server reconciliation, conflict policy, media caching. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (lines 23, 187).
- [ ] **M8 — Multi-server sync and secure network storage.** `SecureNetworkMediaStorageProvider`, master/replica sync, master selection, Windows/Linux mixed-topology verification. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (lines 24, 177-191, 385).
- [ ] **Evidence store backup and disaster recovery.** Considered and explicitly deferred; the server is the sole copy of the evidence. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 458).

## Built but unverified

- [ ] **MAUI native WebAuthn end-to-end test.** `WebAuthnNative.cs` / `WebAuthnCeremonyClient.cs` need a run against a real Windows Hello prompt on a Windows machine. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 21).
- [ ] **MAUI local app-lock verification.** Confirm the `ILocalAuthGate` / Windows Hello gate on a real device. Also decide whether a fresh local unlock should satisfy step-up re-auth (not built). Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 19).
- [ ] **SignalR client side.** `IDownloadStatusSource` abstraction and a MAUI SignalR consumer for `LiveHub`. The hub exists. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 22).
- [ ] **Simple mode browser check.** Never verified in a browser. Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 13).
- [ ] **Update-check feature.** Plan says it ships in the same pass, not deferred. Confirm it landed (check-now panel, `NotifyOnly` / `AutoDownloadAndInstall` setting). Source: [ci-cd-versioning-update-check.md](archive/ci-cd-versioning-update-check.md).
- [ ] **Installer event-log source registration.** Called an unfinished WiX TODO; the Inno Setup replacement should cover it. Confirm in `deploy/windows/`. Source: [installer-overhaul-inno-setup.md](archive/installer-overhaul-inno-setup.md) (line 58).

## Deferred features and gaps

- [ ] **Web Push and MAUI toast notifications.** Web Push needs a service worker, VAPID keys, and a subscription UI. MAUI toast needs the SignalR client. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 18, 259).
- [ ] **MAUI local-process log source (M6).** Optional follow-up for the log viewer. Source: [webapp-log-viewer.md](archive/webapp-log-viewer.md) (lines 30, 131).
- [ ] **Simple mode: MAUI remote-client gaps.** No jamming endpoint or Remote repository, so Simple home shows device events only. The Analyze page has the same gap. Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 14).
- [ ] **Operator details for Admin.** Loads through a SuperAdmin-only call, so Admins see an unauthorized alert above the Display mode section. Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 15).
- [ ] **Fail2ban-style auto-ban.** IP that triggers lockout across multiple accounts gets a temporary ban. Flagged as a fast-follow once layers 1-4 are live. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 67).
- [ ] **LDAP live domain controller integration test.** Out of scope for the phase, not built. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 189).
- [ ] **`CreateNetworkShares` user-management TODO.** `TODO(user-management)` in the Inno Setup script. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 246); `deploy/windows/VideoForensics.iss`.
- [ ] **Self-service password reset by email.** Needs reliable SMTP first. Source: [per-user-login-password-passkey.md](archive/per-user-login-password-passkey.md) (lines 236-240, 363); [per-user-login-integration-test-results.md](archive/per-user-login-integration-test-results.md) (line 212).
- [ ] **Mobile layout optimization.** Pending per CLAUDE.md; separate mobile layout, not MainLayout changes. Source: [per-user-login-password-passkey.md](archive/per-user-login-password-passkey.md) (line 365); [per-user-login-integration-test-results.md](archive/per-user-login-integration-test-results.md) (line 214).
- [ ] **Live view browser video (WebRTC).** Session state and telemetry exist; browser-side SDP/ICE relay is a larger follow-up. Confirm scope before Phase 8. Source: [on-demand-live-view.md](archive/on-demand-live-view.md) (line 259).
- [ ] **MCP per-user and per-case data scoping.** The database is single-tenant. Needed only if multi-location or multi-victim isolation is required. Source: [simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) (line 26).
- [ ] **Cleanup: stale Radzen comments.** Verified: no Radzen references remain in `VideoForensics.Ui.Shared`. Comments in `WebApp/Program.cs`, `MauiApp/MauiProgram.cs`, `IBlazorRenderModeProvider.cs`, `ThemePreferenceService.cs`, and `NavGroups.cs` may still mention Radzen. Source: [ui-framework-migration-syncfusion.md](archive/ui-framework-migration-syncfusion.md) (lines 8-9).
- [ ] **Console app archive.** `src/client/VideoForensics` is slated to move to `archive/` once MAUI reaches parity. Held off by user choice. Source: [maui-blazor-hybrid-conversion.md](archive/maui-blazor-hybrid-conversion.md) (line 8, 381).

## Decided out of scope (reference only, no action)

- Finding #3, bearer token over plaintext HTTP on the LAN. Skipped on user confirmation. Source: [security-perf-fixes.md](archive/security-perf-fixes.md) (lines 9-15).
- Keychain / libsecret credential storage on non-Windows. Source: [security-perf-fixes.md](archive/security-perf-fixes.md) (line 47).
- Azure AD / Entra ID. Out of scope; the org does not use it. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 214).
- SMB integration for MSA / Google. Permanently out of scope. Source: [external-auth-smb-integration.md](archive/external-auth-smb-integration.md) (line 216).

## Excluded

- Abandoned: frontends milestones 9-10 ([frontends-mcp-api-only-architecture.md](archive/frontends-mcp-api-only-architecture.md) line 130).
- Stale, already resolved: "Streaming chat remains deferred" ([simple-mode-embedded-mcp-chat.md](archive/simple-mode-embedded-mcp-chat.md) line 16). Shipped in #189.
