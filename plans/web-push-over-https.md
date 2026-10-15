# Web Push over HTTPS

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN (deployment decision; small optional UX code change)

**Source**: `plans/deferred-items.md`, "Needs verification or a decision": "Web Push over HTTPS. Browsers only register service workers in secure contexts, so plain-HTTP LAN deployments get no push. Deployment decision, not a code change." No archive source.

## Verdict

STILL OPEN. Push is fully wired (`webpush.js` calls `navigator.serviceWorker.register('/push-sw.js')`; `PushEndpoints.cs`, `VapidKeyProvider.cs`, `WebPushNotificationProvider.cs`, `WebPushClient.cs`, `Notifications.razor` all exist). Kestrel in `Program.cs` binds HTTP only (`ListenLocalhost` for Local tier, `ListenAnyIP` for Network/Internet) with no `UseHttps` or certificate config, and nothing in `deploy/windows/` (`VideoForensics.iss`, `README.md`) provisions a certificate. On a plain-HTTP LAN origin `navigator.serviceWorker` is undefined, so `vfWebPush.isSupported` returns false and the page shows the generic `PushNotSupported` status with no hint that HTTPS is the cause. `http://localhost` is a secure context, so push already works on the server machine itself.

## Scope

Decide how (or whether) LAN browsers get Web Push. Greps: `Push`, `ServiceWorker`, `service-worker`, `VapidKey`, `UseHttps`, `certificate`, `Kestrel`, `https` over `src/client/web/VideoForensics.WebApp`, `src/client/ui/VideoForensics.Ui.Shared`, `deploy/windows`. Out of scope: end-to-end push delivery verification (separate deferred item).

## Options

| Option | Effort | Security | Works on LAN? |
|---|---|---|---|
| A. Accept no push on plain-HTTP LAN (document it, improve the message) | S | Best (no new surface) | No; localhost only |
| B. Trusted local certificate (Kestrel HTTPS endpoint, installer-generated/imported cert, per-client trust) | L | Good if per-site cert trusted on each client; weak if users click through warnings | Yes, once each client trusts the cert and uses the hostname |
| C. Reverse proxy with a real cert (Caddy/IIS/Cloudflare Tunnel, public DNS name) | M (per-site ops, not product code) | Good; real CA chain, no client trust step | Yes, if clients reach the proxy hostname (split-DNS for LAN) |
| D. Localhost-only push | S (already true) | Best | No; only on the server box |

## Recommendation

Option A for now, with a small UX fix: when the page is not a secure context, show a specific message ("Push needs HTTPS or localhost") instead of the generic not-supported text, and add a short deploy README section that points admins wanting LAN push to a reverse proxy with a real certificate (Option C). Defer Option B: building certificate generation and trust distribution into the installer is large and fragile, and browsers treat self-signed warnings as a security smell for a forensics product. Network tier still works for everything except push.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/client/web/VideoForensics.WebApp/wwwroot/js/webpush.js` | Add `isSecureContext` check; expose reason (e.g. `getUnsupportedReason`) | Distinguish insecure origin from unsupported browser |
| `src/client/ui/VideoForensics.Ui.Shared/Services/WebPushClient.cs` | Surface the reason | Feed the UI |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/Notifications.razor` | New status/message for insecure context, localized via `L[...]` + `.resx` key | Actionable message (localization required) |
| `deploy/windows/README.md` | Add "Web Push and HTTPS" section | Document the decision and proxy option |

## Test plan

Decision part is manual:
1. On the server machine open `http://localhost:<port>/notifications`; enable push; confirm subscription succeeds.
2. From a LAN device open `http://<server-ip>:<port>/notifications`; confirm the new insecure-context message appears (today: generic not-supported).
3. If Option C is adopted: front the app with a proxy on a real cert, repeat on the LAN device and confirm subscription and a test push.

Code part (TDD, only if the UX fix is taken): first write failing tests in the existing Ui.Shared tests project, e.g. `WebPushClient_InsecureContext_ReturnsInsecureReason()` (Moq `IJSRuntime`) and a bUnit-style `Notifications_InsecureContext_ShowsHttpsMessage()` if bUnit is present; then implement. Run scoped with `dotnet test --filter`.

## Risks and open questions

- Is a Ui.Shared test project with bUnit available? Not verified; if not, test the client class only.
- `UseHttpsRedirection()` is active but no HTTPS port is bound, so redirect is a no-op; a future HTTPS option must reconcile this and HSTS.
- Does the product owner want LAN push enough to support Option C operationally?
- MAUI uses native toast, so it is unaffected.

## Priority recommendation

Low: push is a convenience (other notification channels exist), exposure is nil under Option A, and the only code is a small message fix; a real HTTPS rollout would be the costly part and is not recommended now. Effort: S (A with UX fix); L if Option B were chosen.

## Implementation dispatch

Optional UX fix only: Haiku 5.5 subagent (per project memory), tests first, scoped lite gate.
