# Simple Mode + Embedded MCP Chat

## Context

VideoForensics currently has one unified "operator" UI aimed at investigators, with role-gated nav items (`OperatorRole`: ReadOnly/Review/Admin/SuperAdmin) but no simplified experience for the crime-victim end users who actually view this data. Victims don't need — and are overwhelmed by — device config, provider setup, or raw jamming telemetry. They need a plain-language timeline of what happened to their cameras and their evidence, and a way to ask questions about it in natural language.

The repo already has forensic-analysis tools exposed over MCP (`/mcp` on `VideoForensics.WebApp`, consumed today only by external Claude Desktop via the `VideoForensics.Mcp` stdio bridge). This plan adds (1) a "Simple Mode" layout for victim-facing accounts, and (2) an in-app chat panel that is itself an MCP client against the app's own `/mcp` endpoint, backed by a configurable LLM (Anthropic or an OpenAI-compatible endpoint).

**Explicit constraint from the user: do not change the auth model.** Simple Mode reuses the existing paired-device sign-in and `OperatorRole` system exactly as-is — it is a *display preference*, not a new identity or role tier.

**Known limitation, intentionally out of scope:** MCP tools today take a bare `deviceId`/`locationId` with no per-user/case data scoping (confirmed: `JammingTools`, `TimelineTools`, etc. have no `HttpContext`/claims-based filtering — the whole DB is one tenant). This plan does not add data partitioning. It assumes the existing single-install, single-household deployment model. If multi-location/multi-victim isolation is ever needed, that's a separate follow-up, not part of this plan.

## M1 — Simple Mode layout

**Preference storage (not auth):** Add a `UiMode` enum (`Standard`, `Simple`) as a per-operator preference, persisted the same way other operator-level settings are (check `data.common` `Operator` entity / existing preference columns first — reuse that mechanism rather than inventing a new one). Default `Standard`. An Admin/SuperAdmin can set it when creating/editing a ReadOnly account (so the investigator sets up Simple Mode for the victim); the signed-in user can also toggle it themselves via a settings item, unless locked.

**Layout selection:** Extend `ResponsiveLayout.razor` (`src/client/ui/VideoForensics.Ui.Shared/Layout/ResponsiveLayout.razor`) with a third branch reading the resolved `UiMode` from `PairedSessionState` (same source `MainLayout.razor.cs` already reads `Role` from): if `Simple`, render a new `SimpleLayout.razor`; else keep existing Mobile/Main branch.

**New `SimpleLayout.razor`** (`src/client/ui/VideoForensics.Ui.Shared/Layout/Simple/`): no `SfSplitter`, no settings panel, no nav rail full of investigator tooling. Just:
- A plain-language event timeline (reusing `EventDto`/`MediaItemDto` from `VideoForensics.Api.Contracts`, translated via a small presentation-mapping helper — e.g. jamming telemetry → "Your front door camera was blocked for 12 minutes")
- An evidence/downloads list
- The chat panel (M2), prominent rather than tucked away
- Minimal chrome: sign out, nothing else

Follow the existing `NavGroup`/`NavContext` pattern (`NavGroups.cs`) only if Simple Mode ends up needing more than these 2-3 views — otherwise keep it as plain Razor markup, not a nav-driven page, to avoid dragging in investigator nav semantics.

## M2 — Embedded MCP chat

**Architecture:** the chat backend runs server-side (in `VideoForensics.WebApp`) and is itself an MCP *client* against the app's own existing `/mcp` endpoint — reusing 100% of the existing tool code (`JammingTools`, `TimelineTools`, `SecurityEventTools`, etc.) with zero duplication. This mirrors exactly what `VideoForensics.Mcp/Program.cs` already does today (`McpClient.CreateAsync(httpClientTransport, ...)` then `ListToolsAsync`/tool invocation) — reuse that same `ModelContextProtocol.Client` call pattern, just looped back to `http://localhost/mcp` in-process instead of proxied over stdio to an external Claude Desktop.

**Server side (`VideoForensics.WebApp`):**
- New `ChatEndpoints.cs` (alongside existing `*Endpoints.cs` files) exposing `POST /api/v1/chat` — takes conversation history + new user message, returns assistant reply (+ which tools were called, for transparency/audit).
- New `IChatOrchestrator` service: holds an `McpClient` connected to the local `/mcp` endpoint, lists available tools, and drives a tool-use loop against the configured LLM provider.
- New `ILlmChatProvider` abstraction with two implementations:
  - `AnthropicChatProvider` — Messages API with tool use
  - `OpenAiCompatibleChatProvider` — Chat Completions API with tools, configurable base URL (works for ChatGPT-compatible and self-hosted endpoints)
- **Config, SuperAdmin-only:** new `LlmProviderOptions` (Provider, BaseUrl, Model) via `IConfiguration`/env var, and API key stored using the same pattern as `SmtpPasswordStore.cs` (`src/client/host/VideoForensics.Hosting/SmtpPasswordStore.cs`) — DB-persisted, `ICredentialEncryptionProvider`-encrypted (DPAPI-backed), not plaintext config. New Settings page section for SuperAdmin to enter/rotate the key and pick provider/model, mirroring the SMTP settings page.
- Every tool call the LLM makes is logged the same way other user-facing paths are (per CLAUDE.md: Info on success, Error on failure) for audit purposes, given this touches forensic evidence data.

**Contracts:**
- New DTOs in `VideoForensics.Api.Contracts`: `ChatMessageDto`, `ChatRequestDto`, `ChatResponseDto`.
- New `IChatService` interface in `src/data/common/data.common/Contracts/IChatService.cs` (same location as `IDeviceRepository.cs`, following the exact existing pattern so the client/server split stays consistent).

**Client side (`Ui.Shared` + `Hosting`):**
- `RemoteChatService : IChatService` in `src/client/host/VideoForensics.Hosting/Remote/`, following `RemoteDeviceRepository.cs`'s exact shape: typed `HttpClient`, `PairedDeviceAuthHandler` reused automatically (no new auth plumbing — satisfies "do not change auth"), DTOs in/out, `/api/v1/chat` route. Registered in `AddVideoForensicsClientApi` alongside the ~23 existing typed clients.
- New `ChatPanel.razor` in `Ui.Shared`, injecting `IChatService`. Available in both `MainLayout` (as an optional panel/nav item, gated to at least Admin or whatever role should have it) and `SimpleLayout` (prominent, primary surface).

**Streaming — explicitly deferred:** no SSE/Polly exists in the solution today. MVP ships as a single request/response call (matches how `RemoteDeviceRepository`-style calls already work), not token streaming. Flag streaming as a fast-follow, not part of this plan.

## Execution order (Haiku dispatch, per CLAUDE.md workflow)

Each item below is TDD-first (test written and failing before implementation), one Haiku subagent per file/service, lite gate (incremental build+test) after each:

1. `UiMode` preference — entity/column + repository method + test
2. `ResponsiveLayout.razor` third branch + `SimpleLayout.razor` skeleton
3. Plain-language event/jamming translation helper + tests (pure function, easy to test in isolation)
4. `ChatMessageDto`/`ChatRequestDto`/`ChatResponseDto` in `Api.Contracts`
5. `IChatService` interface in `data.common/Contracts`
6. `ILlmChatProvider` + `AnthropicChatProvider` + `OpenAiCompatibleChatProvider` (with tests mocking the HTTP call)
7. LLM API key store (mirroring `SmtpPasswordStore`) + tests
8. `IChatOrchestrator` (MCP client loop) + tests (mock `McpClient`)
9. `ChatEndpoints.cs` (`/api/v1/chat`) + integration test
10. `RemoteChatService` + registration in `AddVideoForensicsClientApi` + test
11. `ChatPanel.razor` wired into `MainLayout` and `SimpleLayout`
12. SuperAdmin settings page section for LLM provider config

Full gate (clean rebuild + full test suite + package updates) before opening the PR, with user confirmation first, per existing CLAUDE.md workflow.

## Verification

- `dotnet build`/`dotnet test` (lite gate) after each numbered step, scoped to touched projects.
- Manually sign in as a ReadOnly operator with `UiMode=Simple` set → confirm `SimpleLayout` renders instead of `MainLayout`/`MobileLayout`.
- Manually ask the chat panel a question that requires a tool call (e.g. "was my camera jammed this week?") with a test Anthropic key configured → confirm it calls through to the real `/mcp` tools and returns a plain-language answer; check server logs show the tool invocation.
- Confirm `RemoteChatService` calls carry the paired-device bearer token like every other `Remote*` class (no new auth mechanism introduced).
- Full gate + PR only after explicit user go-ahead, per CLAUDE.md.
