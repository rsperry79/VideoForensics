# Plan: SignalR client side with IObservable server data

Source item: [plans/deferred-items.md](deferred-items.md), "Built but unverified → SignalR client side". Status: REVISED after Sonnet review (2026-10-08). Pending user approval before branching.

## 0. Pre-work (phase 0)

- Working tree: `wip` has uncommitted live-view and account-scope changes (`LiveViewSessionOrchestrator.cs`, its tests, `AccountEndpoints.cs`, `Program.cs`, Hosting extensions, `Client.Core.csproj`). User decision: commit these to `wip` first, then branch from `origin/dev`. Do not carry them onto the new branch.
- Before committing, check whether the diff is line-ending churn (git warned LF→CRLF on `LiveViewSessionOrchestrator.cs`). Run `git diff -w --stat` and `git diff --ignore-cr-at-eol --stat`. If it is only CRLF, fix line endings (or `.gitattributes`) and commit the real change only.
- Lite gate for the wip commit: builds of Client.Core, Hosting and WebApp already pass. Still to run: scoped tests `LiveViewSessionOrchestratorTests`, `LiveViewSessionOrchestratorDiTests`, `AccountEndpointsScopeTests`.
- Branch: `feature/signalr-observable-client` from `origin/dev` (`8590d3f` at time of review). Re-verify section 1 before coding.
- **Phase 0 bug fix (red test first):** `LiveHubConnection.StartAsync` (`src/client/host/VideoForensics.Hosting/LiveHubConnection.cs:75`) guards with `if (_connection?.State != HubConnectionState.Disconnected) return;`. On the first call `_connection` is null, so `null != Disconnected` is true and the method returns without connecting. The MAUI hub never starts today. Write a test that the first `StartAsync` builds and starts a connection, watch it fail, then fix the guard (`_connection is null || State == Disconnected`).

## 1. Current state (verified on origin/dev)

- **Client hub connection:** `ILiveHubConnection` / `LiveHubConnection` in `src/client/host/VideoForensics.Hosting/LiveHubConnection.cs`. Singleton in `VideoForensicsHostingExtensions.cs` (~line 668). Exposes C# events `DownloadProgressReceived`, `UrgentEventReceived`. Contains the phase-0 bug above.
- **Reconnect:** uses `WithAutomaticReconnect()` with default retries (0, 2, 10, 30 s), then the connection closes permanently. A server restart leaves the client dead. Needs a custom `IRetryPolicy` and restart-after-`Closed`.
- **Server pushes only two messages** over `LiveHub` (`/hubs/live`):
  - `DownloadProgress` from `DownloadProgressBroadcastService`, every 750 ms, to all clients, sent even when idle.
  - `UrgentEvent` from `SignalRNotificationProvider`.
- **Remote download service** (`src/client/host/VideoForensics.Hosting/Remote/RemoteVideoDownloadService.cs`) subscribes to the hub in its constructor and caches the last payload. It is an `AddHttpClient` typed client (transient), so each instance adds a subscription to a singleton hub: a leak. Its `_activityLog` grows without bound until drained.
- **Activity is a destructive drain.** `IVideoDownloadService.DrainActivityLog()` clears on read. The broadcast drains once per tick for all clients, so a second client loses lines.
- **Web Push is live.** `Program.cs:173` registers `WebPushNotificationProvider` (scoped, `Program.cs:171`). The deferred-items note that Web Push was not built is stale. Correct it.
- **UI polling inventory:**
  - `DownloadProgressPanel.razor`: 500 ms `PeriodicTimer`, calls `DownloadService.GetProgress()`. Takes `IVideoDownloadService` as a `[Parameter]`, not a hub. Used by WebApp Blazor Server (local adapter, no hub) and MAUI (remote service). Needs a host-agnostic source.
  - `RingSelfTest.razor:507`: 500 ms `PeriodicTimer` hitting `GET /api/v1/selftest/status`.
  - `LiveView.razor`: uses a separate hub, `/hubs/liveview-signaling`, through JS interop (`LiveView.razor:97`). Also a 500 ms one-off delay.
  - `ServerLogs.razor`, `AccountDetails.razor`, `ChangePassword.razor`: debounce, backoff or post-action delays only. Not polls.
  - `RemoteLogViewerService.StreamAsync` already streams over SSE, outside SignalR.
- **Consumers to migrate:** `MauiProgram.cs:223-227` subscribes to `UrgentEventReceived` for toasts. `RemoteVideoDownloadServiceTests.cs` mocks `ILiveHubConnection` at 13 sites (lines 54-214).
- **Remote services:** about 35 `Remote*` services exist under `src/client/host/VideoForensics.Hosting/Remote/`. Each must be classified in the inventory (section 2.1).
- **Contract violation:** `DownloadProgressPayload` lives in Hosting and carries `Providers.Common.DownloadStatus`. Client/server split rules require wire payloads to be DTOs in `VideoForensics.Api.Contracts`.

## 2. Goal and scope

Every server-to-client data flow the MAUI client displays as live state is delivered over `LiveHub` and surfaced as `IObservable<T>`. The UI subscribes; it does not poll.

Scope is "all server data" (user choice). Commands and one-shot queries stay HTTP (archived MAUI plan §6: triggering actions is a plain POST). A flow is "state" if the UI keeps showing it over time.

### 2.1 Inventory (completed 2026-10-08, against origin/dev `add2e4c`)

Method: every `Remote*` class under `Hosting/Remote/` and every polling site under `Ui.Shared`. Full detail was produced by a read-only review; summarized here.

**Streams (move to SignalR):**

| Flow | Current transport | UI consumer | Stream name | Phase |
|---|---|---|---|---|
| Download progress | LiveHub `DownloadProgress` (already push; cached in `RemoteVideoDownloadService`) | `DownloadProgressPanel.razor` (500 ms timer over local cache) | `DownloadProgress` | 3 / 5 |
| Urgent security events | LiveHub `UrgentEvent` | `MauiProgram.cs:223` toasts | `UrgentEvents` | 3 / 4 |
| Self-test status and result | HTTP poll, 500 ms (`RingSelfTest.razor:507`, `GET /api/v1/selftest/status`, `/result`) | `RingSelfTest.razor` | `SelfTestStatus` (SuperAdmin-only, §3.2 matrix) | 3 / 5 |
| Live view session state | HTTP poll via `Task.Delay` loop (`LiveView.razor:154`, `GetActiveSessionAsync`) | `LiveView.razor` | `LiveViewTelemetry` (session lifecycle) | 3 / 5 |

**Candidates needing a product call (decide in phase 1 review; default is STAYS_HTTP until confirmed):**

| Flow | Current | Note |
|---|---|---|
| Security events (`RemoteSecurityEventsService`, `SecurityEvents.razor`) | Load once, no timer | Admin-only stream if promoted |
| Jamming incidents and stats (`RemoteJammingRepository`, `SimpleHome`) | Load once | Weak candidate |
| Unanswered events (`RemoteEventRepository`) | Load once | Weak candidate |
| Device health (`RemoteDeviceHealthRepository`, `Evidence.razor`) | One-shot query per page | Not a live flow today; the earlier "DeviceHealth stream" idea is dropped unless a live consumer appears |

**Stays HTTP (commands and one-shot queries):** all other `Remote*` services, including cases, accounts, auth, devices, discovery, media, evidence (validate/export/reconcile), reports, backup/import, storage settings, update check, admin operators, lockout and two-factor policy, and device config.

**Out of scope (already push-based, or a separate hub):**
- Chat reply stream: SSE at `/api/v1/chat/stream` (`RemoteChatService`).
- Server log stream: SSE at `/api/v1/logs/stream` (`RemoteLogViewerService`). Snapshot stays HTTP.
- Live view WebRTC signaling: `LiveViewSignalingHub` at `/hubs/liveview-signaling`. Revisit when unifying hubs.

**Not server polls (no change):** `ServerLogs.razor` (reconnect backoff, render throttle, search debounce); `AccountDetails.razor:259`, `AccountSyncSchedule.razor:395`, `ChangePassword.razor:76` (post-action delays, classified from the line alone; confirm in phase 1 review).

**Open for verification:**
- `RemoteForensicsConfigurationService` has no route string I could match. Check before phase 2.
- `LiveViewSessionService` is not under `Remote/`. Confirm its endpoint and the `LiveView.razor` poll path.
- `ILockoutPolicyService`, `ITwoFactorPolicyService`, `IIntegrityRecordRepository` have no direct UI consumer; may be reached through wrappers.

## 3. Design

### 3.1 Wire contracts (`VideoForensics.Api.Contracts`)

- One file per stream (e.g. `LiveStreamDtos.cs`), each DTO with `ToDto()` / `ToDomain()` mappers (CLAUDE.md naming).
- `DownloadProgressDto` replaces `DownloadProgressPayload`. Fields: progress numbers, current device index/total/name, `LastError`, `RemainingReason` (today's `RemoteVideoDownloadService` returns null for both because the payload lacks them), pre-scan counts, and a sequence number.
- Activity lines become `ActivityEntryDto { long Sequence; string Text; }`. The server keeps a bounded ring buffer and each client tracks the last sequence it has seen. Nothing is drained destructively, so a second client does not lose lines.
- Each message declares its semantics: `Replace` (state snapshot) or `Append` (activity entries).
- Initial stream set: `DownloadProgress`, `UrgentEvents`, `SelfTestStatus`, `DeviceHealth`, `LiveViewTelemetry`, `SecurityEvents`, and `ConnectionState` on the client side only.

### 3.2 Server (WebApp)

- Hub method names as constants in one place (`LiveHubMethods`), shared with the client.
- **Snapshot on connect.** `LiveHub.OnConnectedAsync` sends one `Replace` snapshot per stream to the caller. It runs after the admin-group add. Each reconnect is a new connection, so every reconnect gets a fresh snapshot. This replaces any separate resync path.
- **Send on change.** `DownloadProgress` is sent only when the payload differs from the last one sent. The 750 ms timer stays as the sampler (no per-tick event exists in `VideoDownloadServiceAdapter`). Empty `Activity` and `PreScanCounts` are not sent.
- **Producers.** Other streams publish from their persist points (`DeviceHealthSyncService`, `LiveViewSessionOrchestrator`, `SecurityAuditLogger`), not by polling.
- **Authorization matrix** (explicit, tested):

| Stream | Paired device, ReadOnly | Admin and above (`admins` group) |
|---|---|---|
| DownloadProgress | yes | yes |
| UrgentEvents | yes (today's behavior) | yes |
| SelfTestStatus | no | yes |
| SecurityEvents | no | yes |
| DeviceHealth | yes | yes |
| LiveViewTelemetry | yes | yes |

  Security events and self-test status are sensitive, so ReadOnly devices must not receive them. Confirm this matrix in review.

### 3.3 Client (Hosting and MAUI)

- **Contract:** a new `IRealtimeHub` in a `Contracts/` folder exposing `IObservable<T>` per stream plus `IObservable<ConnectionState> Connection`. `ILiveHubConnection`'s C# events are removed.
- **Rx dependency:** `IObservable<T>` is in the BCL. Only add `System.Reactive` if a needed operator (e.g. `Replay`, `Throttle`) is not trivially hand-rollable. Decide in phase 4; if it is added, use the latest stable version from `dotnet list package --outdated`. Use `Subject.Synchronize()` for thread safety.
- **Singleton store.** A singleton `IRealtimeStore` holds the latest value per stream and is the only subscriber to the hub. Remote services read from the store (getters remain synchronous, so `IVideoDownloadService` does not change). This removes per-instance subscriptions from transient typed clients.
- **Reconnect policy.** Custom `IRetryPolicy` that retries indefinitely with capped backoff. On `Closed` with a non-auth error, restart the connection. On 401, clear the session state and surface `ConnectionState.AuthFailed`.
- **Token.** Do not capture `PairedSessionState` (circuit-scoped) inside a singleton. Resolve the token through an `IAccessTokenSource` abstraction that reads the current session per request.
- **Host-agnostic progress source.** `DownloadProgressPanel` takes an `IDownloadProgressSource` (Ui.Shared Contracts) instead of `IVideoDownloadService`. Local implementation wraps the adapter (WebApp, no hub). Remote implementation wraps `IRealtimeStore`. Panel subscribes to `Observable`; no timer.
- **UI disposal.** Every component subscription is disposed in `Dispose`/`DisposeAsync`.
- **MAUI toast.** `MauiProgram.cs:223-227` migrates to `UrgentEvents.Subscribe`.

### 3.4 Rules

- New public types are interfaces in `Contracts/` folders, with XML docs.
- `HubConnection` stays inside the hub implementation; no SDK types leak.
- Log user-facing paths at Info on success, Error on failure. No raw GUIDs.
- All new UI text uses `IStringLocalizer<SharedResources>`.

## 4. Test plan (TDD: each test written and seen failing first)

Naming: `<Class>_<Scenario>_<Expected>()`.

| Area | Test |
|---|---|
| Phase 0 | `LiveHubConnection_FirstStart_ConnectsToHub` (red until guard fixed) |
| Contracts | DTO round-trip for every stream; `ActivityEntryDto` sequence preserved |
| Server | `LiveHub_OnConnected_SendsOneReplaceSnapshotPerStream` |
| Server | `DownloadProgressBroadcast_UnchangedPayload_NotSent` |
| Server | `DownloadProgressBroadcast_TwoClients_BothReceiveAllActivity` |
| Server | `LiveHub_ReadOnlyDevice_DoesNotReceiveAdminStreams` (authorization matrix) |
| Server | `LiveHub_Unauthenticated_Rejected` (regression) |
| Client store | `RealtimeStore_Emission_UpdatesLatestValue` and `…_BeforeAnyEmission_ReturnsDefault` |
| Client hub | Reconnect after server restart re-subscribes and receives a fresh snapshot |
| Client hub | Retry policy keeps retrying past the default 4-attempt limit |
| Client hub | 401 on connect sets `AuthFailed`, does not loop |
| Client hub | Concurrent emissions are thread-safe (`Subject.Synchronize`) |
| Client hub | Disposing a subscription stops emissions (no leak) |
| Remote cache | `RemoteVideoDownloadService` getters return latest value; `LastError`/`RemainingReason` now populated |
| Remote cache | Activity ring buffer is capped (no unbounded growth) |
| UI (bUnit) | `DownloadProgressPanel` renders on emission, has no timer, disposes subscription |
| Migration | `RemoteVideoDownloadServiceTests` mocks updated for the new contract |

Coverage: interfaces 100%, business logic >80%, integrations >70% (CLAUDE.md).

## 5. Phases

0. **Pre-work.** Resolve the `wip` dirty tree and line-ending check, commit `wip`, branch from `origin/dev`. Red test and fix for the `StartAsync` guard.
1. **Inventory.** Complete section 2.1 by classifying every `Remote*` service and polling `.razor`. Update this file. Stop for user review of the inventory before coding.
2. **Contracts and DTOs.** Mapper tests first. Move `DownloadProgressPayload` to a DTO with the new fields.
3. **Server.** Snapshot-on-connect test, send-on-change test, authorization matrix tests, then each producer (download, urgent, self-test, then the rest).
4. **Client store and hub.** `IRealtimeHub` / `IRealtimeStore`, retry policy, token source, thread-safety. Migrate the 13 test mocks and `MauiProgram`.
5. **Remote services and UI.** Update `RemoteVideoDownloadService`, `IDownloadProgressSource`, `DownloadProgressPanel`, `RingSelfTest.razor`.
6. **Lite gate** after each phase: build touched `.csproj`s, run only new and touched tests (`dotnet test --filter`).
7. **Full gate** before the PR. Ask user for confirmation first (CLAUDE.md).

Each file change is delegated to a Sonnet subagent (`model: "sonnet"`), test-first. The main session verifies by building, running the scoped tests, and reading the diff.

## 6. Resolved open questions (from Sonnet review)

1. **Commands stay HTTP.** Confirmed: hub RPC adds nothing for request/response.
2. **Progress timer kept, send on change.** The adapter has no per-tick event; an event-driven rewrite is a separate task.
3. **Snapshot = current state only.** History (logs, telemetry history) stays HTTP.
4. **Web Push is live.** Correct `deferred-items.md`.
5. **Other polling UI:** `RingSelfTest.razor` (500 ms timer) is in scope. `LiveView.razor` is covered by the separate signaling hub decision. The others are not polls.
6. **Line endings:** check with `git diff -w` and `--ignore-cr-at-eol` in phase 0; fix `.gitattributes` only if the diff is CRLF churn.

## 7. Decisions still needed from the user

- Approve the authorization matrix in §3.2 (in particular: ReadOnly devices do not get SecurityEvents or SelfTestStatus).
- Confirm adding `System.Reactive` is acceptable, or keep to the BCL `IObservable<T>` with hand-rolled operators.
- Approve the phase 1 inventory before coding starts.
