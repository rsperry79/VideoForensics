# MAUI local-process log source (M6)

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. The server log viewer shipped; no local-process source exists.

**Source**: `plans/archive/webapp-log-viewer.md` line 30 (Out of scope: "a local-process log source for the MAUI app (optional follow-up, M6)") and line 131 (Decision 4: "M6 (MAUI local logs) deferred").

## Verdict

STILL OPEN, optional, low value. Grepped origin/dev for `ILogViewerService`, `LogEntry`, `LogQuery`, `LogPage`, `InMemoryLogBuffer`, `AddInMemoryLogBuffer`, `ServerLogs`, `LogEndpoints`. The viewer exists end to end (`ServerLogs.razor` at `/settings/logs`, `ILogViewerService`, `RemoteLogViewerService`, `LogEndpoints`, `InMemoryLogBuffer`), but only as a remote SuperAdmin view of the server process. MAUI only writes a file log (`MauiProgram.cs`: `AddVideoForensicsLogging(logFilePath, ...)`) and has no in-app view of its own logs. The file log under `%ProgramData%/VideoForensics/logs` already covers diagnostics for the optional follow-up.

## Scope

In: an in-app view of the MAUI process's own logs, reusing the existing `ServerLogs` page and `LogEntry`/`LogQuery`/`LogPage` types via a local `ILogViewerService` implementation backed by an in-process ring buffer. MAUI-only registration; the WebApp registration is unchanged.
Out: persisting logs, merging with server logs, any new API routes, mobile layout, and any change to the server viewer's SuperAdmin/step-up rules.

## Code and file touchpoints

| file | change | why |
|---|---|---|
| `src/core/core/core.logging/Services/InMemoryLogBuffer.cs` | none (reused) | ring buffer with `GetSnapshot`/`SubscribeWithBacklog` already exists |
| `src/core/core/core.logging/DependencyInjection/ServiceCollectionExtensions.cs` | reuse `AddInMemoryLogBuffer(capacity)` | already provides the logger provider and buffer |
| `src/client/common/VideoForensics.Client.Common/Contracts/ILogViewerService.cs` | none (reused) | interface and types stay as-is |
| `src/client/maui/VideoForensics.MauiApp/Services/LocalLogViewerService.cs` NEW | `ILogViewerService` over `InMemoryLogBuffer`; maps `LogRecord` to `LogEntry`; ignores step-up token or validates non-empty only | no HTTP, no server call |
| `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs` | call `AddInMemoryLogBuffer`; register `ILogViewerService` as `LocalLogViewerService` after `AddVideoForensicsClientApi` (last registration wins) | MAUI shows its own logs |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/ServerLogs.razor` | possibly a source label or title variant ("Local logs") via a resx key | avoids a misleading "Server" heading; must be localized |
| `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs` | possibly gate or relabel the "Server Logs" nav item on MAUI | route `/settings/logs` is SuperAdmin-gated already |
| `src/client/maui/` test project | verify path before dispatch (no MAUI test project confirmed on origin/dev) | home for `LocalLogViewerServiceTests` |

## Test plan

Tests first, in the MAUI test project if one exists, otherwise `VideoForensics.Hosting.Tests` or a new sibling test project (decide before dispatch).
- `LocalLogViewerService_GetPageAsync_FiltersByMinLevelAndSearch` — fails (type does not exist), then passes.
- `LocalLogViewerService_GetPageAsync_AfterSequence_ReturnsOnlyNewer`
- `LocalLogViewerService_StreamAsync_YieldsBacklogThenLive` and `..._Cancelled_Completes`
- `LocalLogViewerService_GetPageAsync_Truncated_SetsFlag`
- Mapping test: `LogRecord` to `LogEntry` preserves sequence, level, category, redacted message, exception.
- Ui.Shared: `ServerLogsPageTests` extended for the local title key if the page is changed.
Run scoped with `dotnet test --filter` on new classes plus `ServerLogsPageTests`.

## Risks and open questions

- Client-boundary rule: `VideoForensics.Core.Logging.csproj` has a `ProjectReference` to `data.common`, and MAUI already references core.logging. Local logging therefore adds no new violation, but it reinforces a pre-existing one; do not expand it.
- Redaction: reuse `LogRedactor` output so local logs stay as sanitized as the server's.
- Step-up and SuperAdmin gating: local logs are on the operator's own device, but the page still requires step-up; decide whether to keep it (simplest, consistent) or bypass locally.
- Whether the MAUI lifetime of the buffer matters: logs before DI build are not captured.
- Mobile layout is pending; the page is desktop-first.

## Priority recommendation

Low: small product value (file log exists), low security exposure (reuses sanitized buffer, SuperAdmin-gated page), small effort. Effort: S.

## Implementation dispatch

One Sonnet subagent: tests first for `LocalLogViewerService` (confirm failing), then the service and `MauiProgram` registration; a second small dispatch only if the page title/nav relabel with resx keys is wanted.
