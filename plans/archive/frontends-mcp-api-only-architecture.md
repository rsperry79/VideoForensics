# Frontends and MCP hosts go through the WebApp API only, with typed DTOs, dual local/internet server config, real-time push, and MAUI+server debug launch

## Context

Today MAUI bootstraps the *entire* server stack in-process (the console app does the same, but it's slated to be archived soon and is out of scope for this plan — don't design around it): `AddVideoForensicsDataLayer()` + `AddVideoForensicsServerCore()` in [MauiProgram.cs](src/client/maui/VideoForensics.MauiApp/MauiProgram.cs) wire up the local SQLite DB and the Ring provider directly inside the MAUI process, and the shared Blazor pages in `VideoForensics.Ui.Shared` `@inject` domain-level interfaces (`IProviderAuthService`, `IDeviceRepository`, `IReportGenerationService`, etc.) straight from DI. This was a deliberate, explicitly-commented temporary bootstrap ("MAUI talks to the Ring provider directly... until M6"). The user now wants that temporary state closed out: **all frontends (MAUI and, implicitly, Web) must reach the backend only through the `VideoForensics.WebApp` HTTP API**, using **strongly-typed DTOs** rather than raw domain entities on the wire, with **MAUI debug launching the WebApp automatically**, and a **Local-intranet vs. Internet server address setting** that the app can **auto-select based on detected network location**.

Good news: this isn't starting from zero. The target shape is already scaffolded and commented as the intended end state:
- `VideoForensics.WebApp` ([src/client/web/VideoForensics.WebApp](src/client/web/VideoForensics.WebApp)) is already a real ASP.NET Core Minimal API + Blazor Server host, with `Api/MediaApiEndpoints.cs`, `DeviceManagementEndpoints.cs`, `NetworkSettingsEndpoints.cs`, `PairingEndpoints.cs`, `SecurityAuditLogEndpoints.cs`, `NotificationEndpoints.cs`, `RemoteAccessEndpoints.cs`, `ExportDownloadEndpoints.cs`, `HealthEndpoints.cs`, plus a SignalR hub and mDNS LAN advertisement (`Discovery/MdnsAdvertisementService.cs`).
- `AddVideoForensicsClientApi(Uri serverAddress)` already exists in [VideoForensicsHostingExtensions.cs:251](src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs:251), backed by working `HttpClient`-based repos in `src/client/host/VideoForensics.Hosting/Remote/` (`RemoteDeviceRepository`, `RemoteMediaItemRepository`, `RemoteIntegrityRecordRepository`). It's deliberately **not called** by MAUI yet — the code comment at [MauiProgram.cs:130](src/client/maui/VideoForensics.MauiApp/MauiProgram.cs:130) says exactly why (competing DI registrations, no server address until pairing exists).
- A `NetworkTier` enum (Local/Network/Internet) already exists (`data.common/Entities/OperatorRole.cs:28`) but it's a **server-side** concept — how exposed the server's Kestrel bindings are — resolved per-request by `INetworkTierResolver`. It is *not* the client-side "which URL should I dial" setting the user is asking for; that needs a new, separate setting so the two aren't conflated.

The gap: only 3 of the ~20 interfaces the Blazor pages inject (`IDeviceRepository`, `IMediaItemRepository`, `IIntegrityRecordRepository`) have a remote/HTTP implementation and server endpoint. Everything else (`IProviderAuthService`, `IReportGenerationService`, `IEvidenceValidationService`, `IEvidenceExportService`, `IBackupExportService`/`IBackupImportService`, `IDeviceConfigRepository`, `IEventRepository`, `ILegalHoldRepository`, `IUserRepository`, `IProviderAccountRepository`, `IForensicsConfigurationService`, `IDeviceDiscoveryService`, `IEventAndConfigService`, ...) is still resolved in-process only. Also, every existing API endpoint returns raw `data.common` entities (`MediaItem`, `Device`, `IntegrityRecord`) directly — there is no DTO layer decoupling the wire format from internal domain types.

This is a multi-milestone effort (the codebase's own comments call the client/server split "separately scoped, later work" repeatedly). The plan below sequences it so each milestone leaves the app in a working, testable state, rather than one big-bang cutover.

## Milestone 1 — DTO contracts project

Create `src/core/api/VideoForensics.Api.Contracts` (new project, referenced by both `VideoForensics.WebApp` and `VideoForensics.Hosting`/MAUI, referencing nothing else — no EF, no provider SDKs). Per CLAUDE.md convention, its `.csproj` still gets the `Microsoft.CodeAnalysis` package reference like every other project. Add it to `VideoForensics.sln` as part of this milestone, not as a follow-up cleanup step.

**Versioning, decided now rather than retrofitted later:** once MAUI, the converted `VideoForensics.Mcp` (Milestone 9), and the bridge MCP (Milestone 10) all depend on this project's DTOs, a client can end up running against an older or newer server than it was built for — a MAUI install in particular won't always update in lockstep with the server. Route every endpoint under a `/api/v1/` prefix (a plain `MapGroup("/api/v1")` wrapping the existing groups is enough — no need for a versioning NuGet), and treat this contracts project's own assembly version as the thing that has to bump on any breaking DTO change. Nothing consumes multiple versions yet, but the seam needs to exist before there's a second client release, not after.

- Define request/response DTO records mirroring what the API needs to expose — start with the entities already crossing the wire (`DeviceDto`, `MediaItemDto`, `IntegrityRecordDto`), then grow one per new endpoint added in Milestone 2. DTOs are plain records with primitive/enum/DateTime fields only — no `Stream`, no `Func<>`, no EF navigation properties.
- Add explicit mapping extension methods, colocated with each DTO: `entity.ToDto()` (domain entity → DTO, used server-side before `Results.Ok(...)`) and `dto.ToDomain()` (DTO → domain entity, used client-side by `Remote*` classes). Use these exact two names everywhere — not `FromDto`, not a mix — so every DTO file in this plan follows one convention. Keep mapping mechanical and boring — no AutoMapper; this project already avoids extra dependencies per [feedback_prefer_nugets.md](../../.claude/projects/../../../richa/.claude/projects/C--Users-richa-source-repos-VideoForensics/memory/feedback_prefer_nugets.md) only when a NuGet would genuinely save hand-rolled infra, and a handful of static mapping methods doesn't qualify.
- Every DTO and its properties get real XML doc comments (units, valid ranges, what null/empty means), with `GenerateDocumentationFile` turned on for this project — not decoration, this is what makes the OpenAPI schema in Milestone 2 actually self-describing rather than just typed.
- For `IProviderAuthService.AuthenticateWithTwoFactorAsync`'s non-serializable `Func<Task<string>> twoFactorAuthCodeProvider` callback: design this as a two-step DTO exchange, not a mechanical mirror — `POST /api/auth/login` returns `AuthResultDto` with a `RequiresTwoFactor` flag and a short-lived `AuthAttemptId`; if set, the client follows up with `POST /api/auth/login/two-factor { AuthAttemptId, Code }`. Model this in the contracts project as `LoginRequestDto`, `TwoFactorRequestDto`, `AuthResultDto`.
- Update `Api/MediaApiEndpoints.cs` (and any other endpoint currently returning entities) to map to DTOs before `Results.Ok(...)`.

## Milestone 2 — Expand the WebApp API surface to full parity

For every interface a Ui.Shared page currently injects directly, add a matching endpoint group under `Api/`, mirroring the existing style (`RouteGroupBuilder`, `RequireAuthorization` where the interface already implies sensitivity, rate limiting where `MediaApiEndpoints` already does it). Group by page/feature, following the existing one-file-per-feature convention:

- `AuthEndpoints.cs` — wraps `IProviderAuthService` (login, 2FA continuation, status, refresh) per the DTO design above.
- `ReportEndpoints.cs` — wraps `IReportGenerationService`.
- `EvidenceEndpoints.cs` — wraps `IEvidenceValidationService`, `IEvidenceExportService`.
- `BackupEndpoints.cs` — wraps `IBackupExportService`/`IBackupImportService` (streamed request/response bodies, same pattern as the existing media-content streaming endpoint).
- `DeviceConfigEndpoints.cs` — wraps `IDeviceConfigRepository`.
- `EventEndpoints.cs` — wraps `IEventRepository`, `ILegalHoldRepository`.
- `AccountEndpoints.cs` — wraps `IUserRepository`, `IProviderAccountRepository`.
- `ConfigEndpoints.cs` — wraps `IForensicsConfigurationService` (read-only subset needed by clients). **This DTO is an explicit allowlist, not `ForensicsConfiguration` minus a few fields** — the underlying config holds things like the SMTP password (`ISmtpPasswordStore`) and provider account credentials that must never reach a client DTO. Whoever builds this endpoint should read `IForensicsConfiguration`'s actual fields first and name each one that's safe for a client to see, rather than mapping the whole object and trusting DTO field selection to catch secrets later.
- `DiscoveryEndpoints.cs` — wraps `IDeviceDiscoveryService`, `IEventAndConfigService`.

Each endpoint takes/returns DTOs from Milestone 1, never raw entities. Reuse `data.core` orchestrators/services already injected into these endpoints exactly as `MediaApiEndpoints.cs` does today (`IMediaStorageProvider`, etc.) — no new business logic, just an HTTP façade.

**This plan doesn't enumerate every method on every interface above** (`IEventAndConfigService`, `IDeviceDiscoveryService`, `IBackupExportService`, `IEvidenceExportService`, etc.) — their exact method signatures weren't read in full during planning, only that Ui.Shared pages inject them. Whoever builds each endpoint group must open that interface's file in `providers-common/Contracts` or `data.common`/`data.core`'s `Contracts` folder first and design the DTO/routes from the real signature, not from the interface name alone.

**Self-documenting API:** add OpenAPI generation to `VideoForensics.WebApp` via the built-in `Microsoft.AspNetCore.OpenApi` (no extra NuGet — it's in the SDK). Every endpoint added in this milestone gets `.WithSummary()`, `.WithDescription()`, and `.Produces<TDto>()`, not just a route and a lambda — this is what lets Milestone 1's DTO XML docs surface into the schema, and it's what Milestone 8's MCP tools should lean on instead of re-describing the same operation by hand.

**Correlation IDs across the client/server hop:** today a failure is one process's log; once MAUI/MCP calls cross into this API and possibly out to a provider, an operator (or you) needs to connect a client-side error to the matching server-side log line. Use ASP.NET Core's built-in `Activity`/`System.Diagnostics.DiagnosticSource` (already wired into the framework's HTTP client and server instrumentation — no new package) rather than hand-rolling an `X-Correlation-Id` header scheme; make sure the trace/span ID ends up in the structured log output on both ends and in `ISecurityAuditLogger` entries, so a MAUI-reported failure can be matched to server logs directly.

**Auth default:** `MediaApiEndpoints.cs` is unauthenticated today, but that's explicitly flagged in its own doc comment as a temporary M5 state ("must only ever be reachable on a trusted local network... until M6"). Don't carry that forward as the default for new, more sensitive endpoints. Default every new endpoint group in this milestone to `RequireAuthorization` (paired-device auth, matching `DeviceManagementEndpoints.cs`/`NetworkSettingsEndpoints.cs`), and add `AddEndpointFilter<StepUpEndpointFilter>()` to anything destructive or exposure-widening (backup import, evidence export, account/report writes) — the same asymmetry `NetworkSettingsEndpoints.cs` already applies to widening vs. narrowing. Treat "unauthenticated" as an explicit, justified exception per endpoint, not a default to inherit.

## Milestone 3 — Remote (HTTP) client implementations

In `src/client/host/VideoForensics.Hosting/Remote/`, add one `Remote<Interface>` class per interface from Milestone 2, following the existing `RemoteDeviceRepository` pattern exactly (typed `HttpClient`, DTO (de)serialization via `System.Net.Http.Json`, mapping DTOs back to the domain types the interface signature demands), calling the `/api/v1/...` routes from Milestone 1's versioning decision. Extend `AddVideoForensicsClientApi(Uri serverAddress)` in [VideoForensicsHostingExtensions.cs:251](src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs:251) to register all of them, so it becomes the single call that gives a client host full API-backed coverage of every interface Ui.Shared needs — matching what `AddVideoForensicsServerCore()` provides today, minus any provider/DB access.

Each typed `HttpClient` registration gets a `DelegatingHandler` that propagates the current `Activity`'s trace context onto outgoing requests — .NET's `HttpClient`/Kestrel instrumentation does most of this automatically via `Activity.Current`, so this is mostly about not accidentally suppressing it, not building new plumbing.

## Milestone 4 — Cut MAUI over, stop referencing providers/data directly

- In [MauiProgram.cs](src/client/maui/VideoForensics.MauiApp/MauiProgram.cs), replace the `AddVideoForensicsDataLayer()` + `AddVideoForensicsServerCore()` calls with `AddVideoForensicsClientApi(serverAddress)`, where `serverAddress` comes from the new server-location setting (Milestone 5).
- Remove `VideoForensics.MauiApp`'s and `VideoForensics.Ui.Shared`'s `.csproj` references to `providers-common`, `providers-core`, the Ring provider, and `data.common`/`data.core`/`data.database*` — replace with a reference to the new `VideoForensics.Api.Contracts` project only. `VideoForensics.WebApp` keeps its existing direct references (it *is* the server). After removing each reference, do a clean build of just that project (not the whole solution — that's the separate pre-commit gate) to catch any transitive package (e.g. a `Microsoft.Extensions.*` version) that MAUI/Ui.Shared was actually relying on getting pulled in transitively through the dropped references.
- `VideoForensics.Ui.Shared`'s `@inject` lines stay pointed at the same interface names (`IProviderAuthService`, `IDeviceRepository`, ...) — DI now resolves them to the `Remote*` implementations instead of local ones, so no `.razor` changes are needed. This is the payoff of having built Milestone 3 against the same interfaces.
- `VideoForensics.WebApp` itself keeps `AddVideoForensicsServerCore()` unchanged — it remains the one server-tier host. The console app (`src/client/VideoForensics`) is out of scope for this plan entirely: it's slated to be archived soon, so don't spend effort migrating it, keeping it in parity, or designing any of the above around preserving it.

## Milestone 5 — mDNS-only local detection, server-authoritative Internet address

No stored "local server URL" setting — mDNS discovery *is* the local-detection mechanism, so there's nothing to configure or drift out of date for the local case. Only the Internet address needs persisting, and it's authoritative on the server, not the client.

- **Server side:** add an `InternetServerUrl` value to the server's own configuration store (same place/pattern as `IForensicsConfiguration`'s other persisted settings, e.g. alongside `ConfiguredNetworkTier`), settable by a SuperAdmin via a small addition to `NetworkSettingsEndpoints.cs`. **Correction after reading the actual pairing implementation:** `PairingEndpoints.cs`'s QR code just encodes a URL (`{scheme}://{host}/pair?token={token}`, see `/api/pairing/{token}/qrcode.png`) — there's no JSON payload to add a field to. Instead: `register/complete`'s response should include `tierResolver.ResolveTier(context)` for that pairing request; if it resolved to anything other than `Local`, the request's own origin (`{context.Request.Scheme}://{context.Request.Host}`) *is* a working Internet-reachable address, so return it as `initialInternetServerUrl` for the client to cache immediately. If pairing happened at `Local` tier, the client has no Internet URL yet until either a SuperAdmin sets one via `NetworkSettingsEndpoints.cs` (then fetched via the endpoint below) or the device is later paired again from off-LAN. Also expose an *authenticated* `GET /api/v1/client-config` (paired-device auth, same as Milestone 2's default) returning the SuperAdmin-configured `InternetServerUrl` for a paired device to refresh it later — consistent with defaulting new endpoints to authenticated rather than carving out another open one.
- **Client side:** add `IServerLocationSettingsStore` (mirroring `IAppLockPreferencesStore`/`MauiAppLockPreferencesStore`) storing just the single cached value: `CachedInternetServerUrl` (seeded from the pairing payload, refreshed thereafter) — no local URL, no mode enum.
- Add `IServerLocationResolver` in MAUI, called at startup before `AddVideoForensicsClientApi(serverAddress)`:
  1. Browse for the server's mDNS advertisement (`Discovery/MdnsAdvertisementService.cs`'s service, via `Makaretu.Dns`'s browsing API on the client side too — same library both ends, no second mDNS stack) with a short, fixed timeout.
  2. **Found** → use the discovered local address for this session. Since we're on the local network right now, also call the authenticated `GET /api/client-config` against that address and overwrite `CachedInternetServerUrl` in local settings if it changed — this is the "check for updates while connected locally" sync the Internet address needs, since it can only ever be fetched over a connection that already works.
  3. **Not found within timeout** → fall back to `CachedInternetServerUrl` from local settings (only ever empty before the device has completed pairing at least once — surfaced as a "pairing required" state, not a silent failure).
- No `ServerLocationMode`/`ForceLocal`/`ForceInternet` override — mDNS success or failure *is* the switch, matching the user's simplification. Add a small read-only Settings UI section (addition to `Settings.razor`) showing which address is currently active and the cached Internet address, for troubleshooting — not an editable local-address field.
- **TLS for the mDNS-discovered local address:** the WebApp already implies HTTPS (WebAuthn/passkeys need a secure context), and its certificate won't naturally validate against an mDNS `.local` hostname or bare LAN IP — the wrong fix is disabling certificate validation in the client `HttpClient`, which would quietly undo Milestone 2's auth work. Decide this explicitly rather than defaulting into it: either the paired-device credential exchange (Milestone 5's pairing payload) also delivers a pin for the server's specific local-address certificate (validated against that pin, not the system trust store, for local connections only), or local traffic is deliberately plain HTTP with the paired-device bearer token as the actual trust boundary — acceptable only because `NetworkTier.Local` already treats the LAN itself as trusted. Pick one; don't leave it as an unstated gap that gets "solved" later by turning off validation.
- **No pairing-payload versioning needed after the correction above:** since the QR still only ever encodes a bare URL (unchanged), and the new `initialInternetServerUrl` field lives in `register/complete`'s JSON response body (already versioned under `/api/v1/pairing/...` once that route gets the same `/api/v1` prefix treatment as everything else — check whether `PairingEndpoints.cs` was already moved under `/api/v1` by earlier work and do so now if not), this is just an additional field on an existing versioned DTO, not a new payload format to version separately.
- **Fully-offline state:** if mDNS finds nothing *and* `CachedInternetServerUrl` is unreachable (no network at all, or the server is genuinely down), don't let each page fail independently with its own "Cannot resolve service"/HTTP-exception error. `IServerLocationResolver` should surface a distinct "no server reachable" result that the app-level layout checks once at startup/reconnect and routes to a single explicit degraded-state screen, rather than every `Remote*` call site handling it separately.

## Milestone 6 — MAUI debug launches the WebApp automatically

Cross-IDE-safe approach (VS "multiple startup projects" isn't stored in source control, so don't rely on it): add a `#if DEBUG`-guarded startup step in `CreateMauiApp()` that:
1. Probes the configured local server URL's existing `HealthEndpoints` route with a short timeout.
2. If unreachable, launches `VideoForensics.WebApp` as a child process via `System.Diagnostics.Process` (`dotnet run --project <relative path to src/client/web/VideoForensics.WebApp>`), resolving the relative path from `AppContext.BaseDirectory` walking up to the repo/solution root.
3. Polls the health endpoint until it responds (bounded retry/timeout) before proceeding to resolve the server address (Milestone 5) and calling `AddVideoForensicsClientApi`.

This keeps the "launch webserver on MAUI debug" behavior in source-controlled app code rather than a machine-specific IDE setting, so it works the same for every developer.

## Milestone 7 — Connect MAUI to the existing LiveHub instead of polling

There's already a real-time push channel built and explicitly commented as being *for* MAUI, but nothing currently connects to it — worth closing this gap as part of "fully using the API" rather than leaving clients to poll:

- `LiveHub` ([Hubs/LiveHub.cs](src/client/web/VideoForensics.WebApp/Hubs/LiveHub.cs)), mapped at `/hubs/live`, already pushes two events to every connected client: `DownloadProgress` (from `DownloadProgressBroadcastService`, ticking every 750ms) and `UrgentEvent` (from `SignalRNotificationProvider`, fanned out through the existing `INotificationDispatcher`/`INotificationProvider` extensibility point). Both doc comments say this exists "for a remote paired client (MAUI)". `ILiveConnectionTracker` already integrates with device revocation (`DeviceManagementEndpoints.cs`) — the security model already assumes a real hub connection exists.
- Add a client-side `Microsoft.AspNetCore.SignalR.Client` `HubConnection` in `VideoForensics.Hosting`'s client-API registration (`AddVideoForensicsClientApi`), authenticated with the same paired-device token the `Remote*` HTTP repositories already send, pointed at `{serverAddress}/hubs/live`. The server side (`AddSignalR()`/`MapHub<LiveHub>`, [Program.cs:108,262](src/client/web/VideoForensics.WebApp/Program.cs:108)) already lets SignalR negotiate WebSockets automatically; on the client, set `HttpConnectionOptions.Transports = HttpTransportType.WebSockets` explicitly rather than leaving it on auto-negotiate — MAUI's runtime always supports WebSockets, so skipping the SSE/long-polling negotiation step is a straightforward latency win for the 750ms-cadence download-progress stream.
- `IVideoDownloadService`'s progress methods (`GetProgress()`, `GetCurrentDevice()`, `DrainActivityLog()`, `GetPreScanCounts()`, per [DownloadProgressBroadcastService.cs:45-48](src/client/web/VideoForensics.WebApp/Hubs/DownloadProgressBroadcastService.cs:45)) are synchronous/pull-based — don't change that signature. `RemoteVideoDownloadService` (Milestone 3) should hold the *last* `DownloadProgress` payload received from the hub connection in a field, updated by the hub event handler, and have its `GetProgress()`/etc. just return from that cached field — so `CollectVideos.razor`/`CollectSnapshots.razor` keep calling the same synchronous methods they always did, but the values now update because the hub is pushing in the background, not because anything is polling over HTTP.
- Wire `UrgentEvent` into an actual MAUI OS toast/notification. The `AddVideoForensicsClientApi`/`AddVideoForensicsServerCore` doc comments already flag "MAUI toast" as a known, deliberately-unbuilt `INotificationProvider` — this is that gap, closed on the client side by reacting to the hub event rather than adding a new server-side provider. This needs the platform notification-permission prompt/handling on Android/iOS/Windows, not just the `HubConnection` event handler — budget for the permission plumbing, not only the display logic.
- Reconnection/backoff: use `HubConnectionBuilder.WithAutomaticReconnect()` and re-resolve the server address (Milestone 5's local/Internet switch) on reconnect failure, since a dropped hub connection while roaming off/onto the LAN is the same address-switch problem Milestone 5 already solves for plain HTTP calls.

This milestone is independent of Milestone 6 and can be built in parallel with it; it depends on Milestone 4 (MAUI must already be calling `AddVideoForensicsClientApi` with a real, authenticated server address).

## Sequencing note

Milestones 1–3 can land without touching MAUI at all (additive: new project, new endpoints, new Remote classes) and are independently testable against the existing `VideoForensics.WebApp`. Milestone 4 is the only breaking cutover, and should happen only once Milestones 1–3 give it full interface coverage — flipping MAUI over early (before Remote implementations exist for every injected interface) would break pages immediately. Milestones 5, 6, and 7 are MAUI-only additions on top of Milestone 4 and can be built/tested independently of each other. Milestone 8 only needs Milestone 2's auth/audit infrastructure (not 4-7) and can proceed in parallel with the MAUI-side milestones. Milestones 9-10 are abandoned (see below) — Milestone 8 is the final MCP-related milestone.

## Milestone 8 — Host an authenticated MCP endpoint inside the WebApp server

`src/client/VideoForensics.Mcp` currently bootstraps the server stack in-process exactly like MAUI did before Milestone 4 (`AddVideoForensicsDataLayer()` + `AddVideoForensicsServerCore()`, [Program.cs:38-39](src/client/VideoForensics.Mcp/Program.cs:38)), and its `[McpServerToolType]` tools (`TimelineTools`, `IntegrityTools`, `CorrelationTools`, `AuditTrailTools`, `JammingTools`) touch repositories directly with no authentication at all — stdio has none. Rather than exposing that process to a network, move the MCP endpoint itself into the one place that already has an auth/audit story:

- Add MCP HTTP hosting to `VideoForensics.WebApp` using the same `ModelContextProtocol` SDK already in use, via its ASP.NET Core hosting support (`AddMcpServer().WithHttpTransport()`), mapped at `/mcp` alongside the existing `Api/` endpoints in `Program.cs`.
- Move the tool classes (`Tools/*.cs`) and their repositories (`ITimelineRepository`, `IIntegrityRepository`, `ICorrelationRepository`, `IAuditTrailRepository`) directly into `VideoForensics.WebApp` (a new `Mcp/` folder alongside `Api/` — not a separate project; nothing else will consume them once `VideoForensics.Mcp` itself stops holding logic in Milestone 9) so they run in the same process as the REST API and resolve the same DI-registered services `AddVideoForensicsServerCore()` already provides — no new business logic, just a new host for logic that already exists.
- Require the same paired-device authentication and `RequireAuthorization` policy the Milestone 2 endpoints use for `/mcp`; apply `StepUpEndpointFilter` to any tool with write/export effects, matching Milestone 2's auth-default rule.
- Wrap tool invocation with `ISecurityAuditLogger` so every MCP tool call is attributed to a paired device/operator the same way REST calls already are — this is what makes it safe to eventually widen `NetworkTier`, unlike the stdio process which has no caller identity at all.
- For any new tool that's a thin wrapper over an existing REST operation (as opposed to `TimelineTools`/`CorrelationTools`-style multi-step analytical queries), pull its MCP tool description from that operation's Milestone 2 OpenAPI summary/description instead of writing a fresh one — keeps the two surfaces from drifting into inconsistent descriptions of the same thing.
- Give `/mcp` its own rate-limiting policy (`RequireRateLimiting`, same mechanism `MediaApiEndpoints.cs`'s "media" policy already uses), separate from and likely stricter than typical REST endpoints — an LLM driving MCP tool calls can issue `TimelineTools`/`CorrelationTools`-style analytical queries in a tight loop in a way a human clicking through the UI won't, and those queries are the more expensive ones in this system.

## Milestones 9-10 — ABANDONED: no local relay/bridge process

**Investigated and dropped.** The original design called for converting `VideoForensics.Mcp` into a thin stdio↔HTTP relay (Milestone 9) plus a non-local variant of the same relay (Milestone 10). A real implementation attempt found that `ModelContextProtocol` v2.2.0 (the SDK this repo uses) has **no dynamic/runtime tool-registration API** on its server side — `AddMcpServer()` only supports attribute-based (`[McpServerTool]`) discovery at startup, with no `ListToolsHandler`/`CallToolHandler`-style hook to back a local stdio server's tool list from a remote `McpClient` at runtime (confirmed by inspecting `McpServerOptions`'s actual public surface — no such member exists). The client side works fine (`McpClient` + `HttpClientTransport` successfully connects to `/mcp` and calls `ListToolsAsync()`), but there's no supported way to re-expose that over a local stdio server without either a ~1000+ line custom `IMcpTransport` or hand-rolling raw JSON-RPC byte forwarding.

**Decision:** skip both milestones rather than build either substantial workaround. `VideoForensics.Mcp` was reverted to its original, unmodified form (`git checkout` on the whole directory) — it remains a standalone, local-only, unauthenticated stdio MCP server with its own DB/provider access, exactly as it was before this plan started touching it. This is a known, accepted limitation, not a fixed problem: it still duplicates provider/DB access outside the WebApp API, same as it always did. Milestone 8's `/mcp` on `VideoForensics.WebApp` is the one authenticated, audited MCP surface; any MCP client capable of connecting to a **remote** MCP server directly over HTTP (increasingly common as the MCP spec's Streamable HTTP transport matures) can use it without any bridge process. A future revisit of this decision should start from whether a newer `ModelContextProtocol` SDK version adds the missing dynamic-registration hook, not from hand-rolling JSON-RPC.

## Verification

- After Milestones 1–3: run `VideoForensics.WebApp` (`dotnet run --project src/client/web/VideoForensics.WebApp`) and hit each new endpoint (via `curl`/Postman or a quick console-app smoke test using the new `Remote*` classes) to confirm DTOs round-trip correctly.
- After Milestone 4: run the MAUI app pointed at a running WebApp instance; walk through Sign-in, Dashboard, Collect Videos, Events, and Reports pages (the ones with the most direct injections) to confirm no page throws from `Cannot resolve service` and that Blazor Server (Web) still works unchanged (it keeps `AddVideoForensicsServerCore()`).
- After Milestone 5: run MAUI on the same LAN as the WebApp and confirm it resolves the mDNS-discovered local address and refreshes `CachedInternetServerUrl`; then run it off that LAN (mDNS timeout expected) and confirm it falls back to the cached Internet address from the previous run.
- After Milestone 6: stop any already-running WebApp, launch MAUI under the debugger, and confirm the WebApp process starts automatically and MAUI's pages load once it's healthy.
- After Milestone 7: kick off a download from MAUI and confirm progress updates arrive via the hub (not polling) and that revoking the paired device (`DeviceManagementEndpoints`) actually drops the live connection.
- After Milestone 8: call `/mcp` unauthenticated and confirm it's rejected; call it with a valid paired-device credential and confirm a tool call succeeds and produces an audit log entry via `ISecurityAuditLogger`.
- Milestones 9-10: N/A — abandoned, see above. `VideoForensics.Mcp` was reverted to its original committed state; verify with `git status src/client/VideoForensics.Mcp/` showing clean, and `dotnet build src/client/VideoForensics.Mcp/VideoForensics.Mcp.csproj` succeeding as it did before this plan touched it.
- Follow this repo's standing gate ([feedback_clean_rebuild_before_commit.md]): before commit/push, ask the user to confirm, then `dotnet clean` + `dotnet build` on the whole solution and `dotnet test` for the full suite, fixing all warnings/errors/failures surfaced — not just ones touching these changes.
- Per [feedback_haiku_subagent_dispatch.md], each milestone's file/service changes should be dispatched to Haiku subagents individually rather than written directly in the main session.

## Subagent dispatch guide

Per [feedback_haiku_subagent_dispatch.md], the main session plans and reviews only — every file/service change below goes to a Haiku subagent, one per bullet (or a small batch of clearly related files where a milestone lists several near-identical items, e.g. the nine endpoint groups in Milestone 2). Each dispatch prompt should be self-contained: it won't have this conversation's context, so include the specific file path(s), the pattern to follow (name the existing file that already does this, e.g. "follow `RemoteDeviceRepository.cs` exactly"), and the relevant cross-cutting rules from below rather than assuming the subagent has read this whole plan.

Suggested task breakdown, in dependency order:

1. **Milestone 1:** one task for the new `VideoForensics.Api.Contracts` project + solution file entry; one task per DTO group (device/media/integrity; auth/login; then one per Milestone 2 endpoint group as it's added) — DTOs can be added incrementally alongside the endpoint that needs them rather than all upfront.
2. **Milestone 2:** one task per endpoint group file (`AuthEndpoints.cs`, `ReportEndpoints.cs`, `EvidenceEndpoints.cs`, `BackupEndpoints.cs`, `DeviceConfigEndpoints.cs`, `EventEndpoints.cs`, `AccountEndpoints.cs`, `ConfigEndpoints.cs`, `DiscoveryEndpoints.cs`) plus its DTOs — nine independent tasks, all following `MediaApiEndpoints.cs`'s existing shape.
3. **Milestone 3:** one task per `Remote<Interface>` class, following `RemoteDeviceRepository.cs` — independent tasks, one per Milestone 2 endpoint group's interface(s).
4. **Milestone 4:** a single task (this one's a coordinated cutover, not independently parallelizable) — `MauiProgram.cs` DI swap + the two `.csproj` reference changes together, since they have to land atomically or the build breaks.
5. **Milestones 5-7:** each bullet point under these milestones is close to one task (`IServerLocationSettingsStore`, `IServerLocationResolver`, the Settings UI addition, the debug-launch startup step, the `HubConnection` wiring, the toast handler) — mostly sequential within a milestone since they build on each other, but the three milestones can run as three parallel tracks once Milestone 4 lands.
6. **Milestone 8:** one task for the `/mcp` HTTP hosting + auth wiring, one for moving each tool class over (done). **Milestones 9-10:** abandoned — no tasks (see the milestone section above for why, and note `VideoForensics.Mcp` was reverted to its pre-plan state via `git checkout`, not left in the half-converted state an earlier attempt produced).

Escalate a task to Sonnet only if the dispatched Haiku subagent reports it's genuinely blocked (ambiguous existing code, can't locate a call site) — not preemptively.

## Client requirements

DONE — moved into `CLAUDE.md`'s new "Client Requirements (client/server split)" section, so every subagent picks it up automatically from project instructions without needing this plan file pasted into its prompt. Client-side dispatch prompts don't need to repeat these rules — just note "per CLAUDE.md's Client Requirements section" if a reminder is useful.

## Templates for subagents

Concrete skeletons, lifted from the actual existing patterns in this repo, to paste into a dispatch prompt so a Haiku subagent isn't inferring shape from prose. Fill in `<...>` placeholders; the surrounding structure and doc-comment style should match as-is.

**DTO (Milestone 1)** — plain record, XML-documented, no domain types:

```csharp
namespace VideoForensics.Api.Contracts;

/// <summary><TYPE_SUMMARY></summary>
/// <param name="Id">The <TYPE>'s unique identifier.</param>
/// <param name="<FIELD>"><FIELD_DESCRIPTION, including units/valid range/null meaning></param>
public record <Type>Dto(Guid Id, <FieldType> <Field>);

public static class <Type>DtoMapping
{
    public static <Type>Dto ToDto(this VideoForensics.Data.Common.Entities.<Type> entity) =>
        new(entity.Id, entity.<Field>);

    public static VideoForensics.Data.Common.Entities.<Type> ToDomain(this <Type>Dto dto) =>
        new() { Id = dto.Id, <Field> = dto.<Field> };
}
```

**Endpoint group (Milestone 2)** — one file per feature, mirroring `MediaApiEndpoints.cs`/`DeviceManagementEndpoints.cs`:

```csharp
using VideoForensics.Api.Contracts;
using VideoForensics.<SomeContractsNamespace>;

namespace VideoForensics.WebApp.Api
{
    /// <summary><ENDPOINT_GROUP_SUMMARY - what it wraps and why, auth posture stated explicitly></summary>
    public static class <Feature>Endpoints
    {
        public static void Map<Feature>Endpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/<feature>")
                .RequireAuthorization() // paired-device auth - see Milestone 2's auth-default rule
                .RequireRateLimiting("<policy-name>");

            _ = group.MapGet("/", async (I<Interface> service, CancellationToken ct) =>
                    Results.Ok((await service.ListAsync(ct)).Select(x => x.ToDto())))
                .WithSummary("<one-line summary an MCP tool description can reuse verbatim>")
                .WithDescription("<longer description: parameters, side effects, error cases>")
                .Produces<IReadOnlyList<<Type>Dto>>();

            // Destructive/exposure-widening operation - add step-up per Milestone 2's asymmetry rule:
            _ = group.MapPost("/<action>", async (<Type>RequestDto request, I<Interface> service, CancellationToken ct) =>
                {
                    await service.<Action>Async(request.<Field>, ct);
                    return Results.Ok();
                })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithSummary("<summary>");
        }
    }
}
```

**Remote HTTP client (Milestone 3)** — mirrors `RemoteDeviceRepository.cs` exactly:

```csharp
using System.Net.Http.Json;

using VideoForensics.Api.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="I<Interface>"/> calling the server's Minimal API
    /// (VideoForensics.WebApp/Api/<Feature>Endpoints.cs) instead of a local implementation -
    /// part of the client/server split (plan Milestone 3).
    /// </summary>
    public class Remote<Interface> : I<Interface>
    {
        private readonly HttpClient _httpClient;

        public Remote<Interface>(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<<DomainType>>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/<feature>", ct);
            _ = response.EnsureSuccessStatusCode();
            List<<Type>Dto>? dtos = await response.Content.ReadFromJsonAsync<List<<Type>Dto>>(cancellationToken: ct);
            return (dtos ?? []).Select(d => d.ToDomain()).ToList();
        }

        // Any interface member with no server endpoint yet: throw NotSupportedException with the
        // same message RemoteDeviceRepository.cs uses, not a silent no-op.
    }
}
```

Registration addition to `AddVideoForensicsClientApi` ([VideoForensicsHostingExtensions.cs:251](src/client/host/VideoForensics.Hosting/VideoForensicsHostingExtensions.cs:251)):

```csharp
_ = services.AddHttpClient<I<Interface>, Remote<Interface>>(c => c.BaseAddress = serverAddress);
```

**Test skeleton** — one per new class, sibling `tests/` project, xUnit + Moq only where mocking is actually needed:

```csharp
public class Remote<Interface>Tests
{
    [Fact]
    public async Task ListAsync_ServerReturnsDtos_MapsToDomainType()
    {
        // Arrange: FakeHttpMessageHandler or a similar test double returning a canned DTO JSON body
        // (see src/providers/ring/video/tests/FakeHttpMessageHandler.cs for the existing pattern).
        // Act: call the method under test.
        // Assert: the returned domain objects match the DTO's values field-for-field.
    }
}
```
