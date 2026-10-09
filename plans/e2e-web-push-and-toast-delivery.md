# End-to-end Web Push and MAUI toast delivery

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. All code exists; nothing has been sent against a live server, and the send path has no automated tests.

**Source**: `plans/deferred-items.md` line 26 ("Neither has been sent against a live server"). No archive source.

## Verdict

STILL OPEN. Server send path (`WebPushNotificationProvider`, `SignalRNotificationProvider`, `NotificationDispatcher`), browser subscription (`webpush.js`, `push-sw.js`, `WebPushClient`, `PushEndpoints`) and MAUI toast (`MauiProgram.cs` `UrgentEventReceived` -> `Toast.Make`) are all present. A grep of tests on origin/dev finds no test for `WebPushNotificationProvider`, `PushEndpoints`, `VapidKeyProvider` or `SignalRNotificationProvider`; only `PushSubscriptionRepositoryTests` exists. `WebPushNotificationProvider` does `new WebPushClient()` inline, so it cannot be tested with a fake push service without a small seam.

## Scope

- In: a manual live-server script for browser push and MAUI toast; automated tests for the send path (fake push sender, preference filtering, 404/410 pruning, audience handling), the endpoints and the SignalR provider.
- Out: HTTPS deployment for LAN installs (separate deferred item "Web Push over HTTPS"; this script uses `https://localhost` or a real cert as a prerequisite); `NotificationAudience.All` web push (currently a logged no-op, see risks).

## Manual verification script

Prereqs: server reachable over HTTPS (service workers need a secure context; `localhost` also qualifies), an Admin login, Chrome or Edge, a Windows MAUI build paired to the same server.

1. Start the server; sign in as an Admin in the browser. Expected: UI loads and `GET /api/v1/push/vapid-public-key` returns 200 with `{ "publicKey": "..." }` (DevTools Network). Restart the server and repeat; the key is identical (persisted in the `WebPush.VapidPublicKey` app setting).
2. Open the Notifications page (`Notifications.razor`) and enable push. Expected: browser permission prompt; on Allow, `/push-sw.js` shows as activated in DevTools > Application > Service Workers; `POST /api/v1/push/subscribe` returns 200; a PushSubscriptions row appears with your OperatorId.
3. Ensure the operator's notification preferences have PushEnabled = true and MinimumSeverity at or below the test event's severity.
4. Trigger an urgent AdminsOnly event (whichever event path calls `NotificationDispatcher`, for example a lockout). Expected: an OS notification with title = event type and body = details; server log "Web push sent successfully" with a truncated endpoint.
5. Close all browser tabs for the site and trigger again. Expected: the notification still arrives (service worker handles it); clicking it behaves as `push-sw.js` defines.
6. Set PushEnabled = false and trigger again. Expected: no notification; debug log "Skipping push".
7. Set MinimumSeverity above the event's severity and trigger. Expected: no notification.
8. Unsubscribe in the UI. Expected: `POST /api/v1/push/unsubscribe` returns 200 and the row is removed. Then re-subscribe, revoke the permission in browser site settings (or clear the subscription browser-side), and trigger. Expected: push service returns 404/410, log "Push subscription expired", row removed.
9. Sign in as a non-admin operator with a subscription and trigger an AdminsOnly event. Expected: no notification for that operator (only `ListForAdminsAsync` subscriptions are targeted).
10. MAUI: launch the paired Windows app (hub started via `ILiveHubConnection.StartAsync`). Trigger an AdminsOnly event as an admin session. Expected: a toast with the `Details` text (falls back to `EventType`, then "Security event"); the SignalR provider sends to the `admins` group.
11. MAUI as a non-admin pairing: trigger an AdminsOnly event. Expected: no toast. Trigger an `All` audience event. Expected: toast (Clients.All).
12. MAUI offline: stop the server mid-session, restart. Expected: the hub reconnects and later events toast again; record whether events during the gap are lost.
13. Record results (OS, browser version, pass/fail per step) in the PR description, including the step 12 gap behavior as a finding.

## Code and file touchpoints

| file | change | why |
|---|---|---|
| `src/client/web/VideoForensics.WebApp/Hubs/WebPushNotificationProvider.cs` | Extract an `IWebPushSender` (thin wrapper over `WebPush.WebPushClient.SendNotificationAsync`) injected via constructor; behavior unchanged | Allows a fake push service in tests |
| `src/client/web/VideoForensics.WebApp/Program.cs` | Register `IWebPushSender` next to the provider registration (line ~173) | DI for the seam |
| `src/client/web/VideoForensics.WebApp/Services/VapidKeyProvider.cs` | None (tested as is) | Needs coverage |
| `src/client/web/VideoForensics.WebApp/Api/PushEndpoints.cs` | None (tested as is) | Needs coverage |
| `src/client/web/VideoForensics.WebApp/Hubs/SignalRNotificationProvider.cs` | None (tested as is) | Needs coverage |
| `src/client/web/VideoForensics.WebApp.Tests/` (new `Hubs/WebPushNotificationProviderTests.cs`, `Services/VapidKeyProviderTests.cs`, `Api/PushEndpointsTests.cs`, `Hubs/SignalRNotificationProviderTests.cs`) | New test files in the existing test project | Send path, key persistence, endpoints, group routing |
| `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs` | Optional: move the inline toast handler into a small testable class | The lambda is untestable; only worthwhile if a MAUI test project exists (not verified) |

## Test plan

TDD: write the tests first against the new `IWebPushSender` interface (they fail to compile until the seam exists), then extract the seam. Project: `src/client/web/VideoForensics.WebApp.Tests` (xUnit v3 + Moq).

- `WebPushNotificationProvider_SendAsync_AdminsOnlyWithEnabledPreference_SendsPayloadWithTitleBodySeverity`
- `WebPushNotificationProvider_SendAsync_PushDisabled_SkipsSubscription`
- `WebPushNotificationProvider_SendAsync_NoPreferences_SkipsSubscription`
- `WebPushNotificationProvider_SendAsync_SeverityBelowMinimum_SkipsSubscription`
- `WebPushNotificationProvider_SendAsync_Sender404Or410_RemovesSubscription` (Theory)
- `WebPushNotificationProvider_SendAsync_SenderOtherError_LogsAndContinuesToNextSubscription`
- `WebPushNotificationProvider_SendAsync_NoSubscriptions_DoesNotLoadVapidKeys`
- `WebPushNotificationProvider_SendAsync_AllAudience_SendsNothing` (documents the current no-op)
- `VapidKeyProvider_GetOrCreateKeysAsync_MissingKeys_GeneratesAndPersistsBoth`
- `VapidKeyProvider_GetOrCreateKeysAsync_ExistingKeys_ReturnsStoredWithoutWriting`
- `PushEndpoints_Subscribe_NoOperatorClaim_ReturnsUnauthorized` and `PushEndpoints_Subscribe_ValidClaim_StoresSubscriptionForOperator`
- `PushEndpoints_Unsubscribe_RemovesByEndpoint`
- `SignalRNotificationProvider_SendAsync_AdminsOnly_SendsToAdminsGroup` and `SignalRNotificationProvider_SendAsync_All_SendsToAllClients`

Expected initial failure: build error on the missing `IWebPushSender`. Scoped run: `dotnet test --filter "FullyQualifiedName~WebPushNotificationProviderTests|FullyQualifiedName~VapidKeyProviderTests|FullyQualifiedName~PushEndpointsTests|FullyQualifiedName~SignalRNotificationProviderTests"`.

## Risks and open questions

- Security: `PushEndpoints` unsubscribe removes any endpoint by value with no ownership check (endpoints are unguessable, but verify and add a test if tightened). Subscribe has no role check; admin-only targeting relies on `ListForAdminsAsync` at send time. Payload carries event details; the push service sees only ciphertext (RFC 8291).
- The VAPID subject is hard-coded `mailto:admin@videoforensics.local`; some push services (Apple) may reject it. Test on Safari/iOS if supported.
- `NotificationAudience.All` is a no-op for web push; confirm that is intended.
- A role downgrade may leave a stale admin subscription; confirm `ListForAdminsAsync` re-checks role.
- SignalR events during a disconnect are lost; push is the durable channel. Confirm in step 12.
- Toast is verified only as Windows code (`Toast.Make`); Android/iOS behavior is unverified.
- No MAUI test project verified on origin/dev, so toast logic stays manual.

## Priority recommendation

Medium: urgent security alerts are a core feature that has never been exercised, the tests are cheap and the unsubscribe-ownership question is a real if small exposure, but nothing is known to be broken. Effort: S for tests plus the seam, M including the live browser and MAUI device passes.

## Implementation dispatch

Dispatch to a `haiku` subagent (per project memory): write the test files first and confirm they fail, extract `IWebPushSender`, register it, confirm they pass; lite gate on `VideoForensics.WebApp` and `VideoForensics.WebApp.Tests`. The manual script is run by the user.
