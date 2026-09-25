# VideoForensics Development Guidelines

## Project conventions

- Every `.csproj` in this repo must reference the `Microsoft.CodeAnalysis` NuGet package, even if the project doesn't use it directly. When creating a new C# project, add `<PackageReference Include="Microsoft.CodeAnalysis" Version="5.9.0" />` (match the version already used by sibling projects) to its `ItemGroup` of package references.

## Core Principles

**All public APIs must be interfaces in `Contracts/` folders.** Implement with xUnit + Moq tests for every interface contract. Use `Microsoft.Extensions.*` packages for cross-cutting concerns; avoid vendor SDK leakage outside service layers.

## Project Structure

Projects live nested under `src/`, grouped by layer, not as flat top-level folders:

```
src/
  clients/VideoForensics/                    # Console app
  core/providers/providers-common/           # Platform-agnostic interfaces (Contracts/)
  core/providers/providers-common-tests/     # Contract tests
  core/providers/providers-core/             # Base classes (e.g. BaseVideoProvider)
  core/providers/providers-core-tests/
  providers/ring/provider/                   # Ring provider implementation
  providers/ring/provider/tests/
  providers/ring/auth/, common/, core/, snapshots/, utils/, video/   # Ring sub-services, each with its own project + tests/
  providers/wyze/provider/                   # Wyze provider implementation
  providers/wyze/provider/tests/
  providers/wyze/auth/, common/, core/, utils/                       # Wyze sub-services, each with its own project
  data/common/, data/core/, data/database/, data/database/sqlite/    # Data access layer
```

There is no `archive/` directory in this repo — don't assume one exists.

## Adding a New Provider

1. Do NOT create new interfaces — reuse `VideoForensics.Providers.Common.Contracts.*` (in `src/core/providers/providers-common/Contracts/`)
2. Create `src/providers/<vendor>/provider/` with four service classes:
   - `<Vendor>AuthService : IProviderAuthService`
   - `<Vendor>DeviceDiscoveryService : IDeviceDiscoveryService`
   - `<Vendor>MediaDownloadService : IMediaDownloadService`
   - `<Vendor>EventAndConfigService : IEventAndConfigService`
3. Implement `<Vendor>VideoProvider : BaseVideoProvider`
4. Add comprehensive tests in a sibling `tests/` project

## Code Standards

- **Async/await by default** for all I/O with `CancellationToken`
- **All async methods accept `CancellationToken`** parameter
- **No vendor SDK outside service layers** — abstract via interfaces
- **Error handling:** log errors with context, expose via `GetLastError()` method, display to users
- **User-facing paths:** always log (Info on success, Error on failure)
- **No secrets in code** — use config/env vars/credential stores
- **Input validation** at API boundaries only
- **No plain-text passwords** — use provider APIs or hash + salt

## Data Requirements

- **All data from provider APIs must be recorded in the database.** Do not rely on JSON blobs or ephemeral storage. Provider responses must be parsed and stored in proper database columns/tables with appropriate schema. Raw JSON is only acceptable for metadata that doesn't fit the schema or for audit trail purposes.
- **No unstructured JSON in core domain tables** — events, devices, locations, and other business entities must have structured schemas. JSON should be limited to optional metadata fields (e.g., `MetadataJson` for provider-specific data that can't be schema-normalized).
- **Eliminate data duplication** — do not scope entities unnecessarily to provider accounts if they represent globally unique provider resources (e.g., ProviderLocationId is globally unique per provider, not per account). Use proper normalization and junction tables for multi-account sharing.
- **Design the schema first** — if you're storing JSON because the schema is incomplete, fix the schema instead. Every provider API response field should have a home in the database.

## Testing

- **Every non-test code change ships with tests covering it.** This applies to new classes, new methods, and behavior changes to existing code (e.g. new branches, fallback paths, retry logic) — not just new interfaces. "Existing tests still pass" is not sufficient proof of coverage for new behavior; if a change adds a new code path, add a test that exercises that path. Treat a change with no accompanying test as incomplete, not as a follow-up to do later.
- **Write code TDD-style: test first, then implementation.** For new behavior (a new class, method, branch, fallback/retry path, or bugfix), write the test(s) that describe the expected behavior before writing the code that satisfies them — the test should fail for the right reason first, then the implementation makes it pass. When dispatching to a Haiku subagent, tell it to write the test(s) first and confirm they fail, then implement, then confirm they pass — don't dispatch "implement X" and "test X" as if testing were an afterthought tacked onto a finished implementation.
- Location: `<project>/tests/<Feature>Tests.cs` (sibling `tests/` project next to the implementation)
- Framework: xUnit (v3) with Moq where mocking is actually needed — not every test project requires Moq
- Naming: `<Class>_<Scenario>_<Expected>()`
- Coverage: interfaces 100%, business logic >80%, integrations >70%
- Run all tests: `dotnet test`

## Documentation

- **Public interfaces:** XML documentation comments
- **Complex logic:** add "why" comments
- **Non-obvious behaviors:** document assumptions
- **Breaking changes:** update this file and commit message

## Claude Communication Standards

Respond to the user with **terse, direct output**:
- One sentence on what changed (max two sentences)
- No trailing summaries or narration
- No hedging or verbose explanations
- Let diffs and tool output speak for themselves

## Archive and docs directories

Do not read or explore files in the `docs/` directory unless explicitly asked by the user. (There is currently no `archive/` directory in this repo — if one is added later, the same rule applies to it.)

## Client Requirements (client/server split)

Applies to any work touching `VideoForensics.MauiApp`, `VideoForensics.Ui.Shared`, `VideoForensics.Mcp`, or an MCP bridge process. Does not apply to `VideoForensics.WebApp` (it *is* the server). See the client/server split plan for full milestone context if one is active.

- **No direct provider/data access from any client host.** A "client host" is MAUI, `VideoForensics.Mcp`, and any MCP bridge process. None of them may reference `providers-common`, `providers-core`, any concrete provider (Ring, etc.), or `data.common`/`data.core`/`data.database*` in their `.csproj`. They may reference `VideoForensics.Api.Contracts` (DTOs) and `VideoForensics.Hosting` (for `AddVideoForensicsClientApi` and its `Remote*` implementations) only.
- **Interface names don't change.** `VideoForensics.Ui.Shared`'s `@inject IProviderAuthService`, `@inject IDeviceRepository`, etc. stay exactly as-is — a `Remote*` class implements the same interface a local implementation did, so DI substitution is silent and no `.razor` file needs editing for a client/server cutover.
- **All wire calls use DTOs from `VideoForensics.Api.Contracts` and hit versioned `/api/v1/...` routes**, never a bare domain entity and never an unversioned route.
- **Every `Remote*`/HTTP-backed class takes and forwards `CancellationToken`**, matching the interface signature it implements — no swallowing it.
- **Auth:** every outgoing call carries the paired-device credential (bearer token), the same way as any existing `Remote*` repository — don't add a new auth mechanism per class.
- **Local vs. Internet server address is never a client-editable setting** — only a cached Internet URL is stored client-side; the local address is always discovered fresh (e.g. via mDNS), never persisted.
- **No stdio MCP process may bootstrap `AddVideoForensicsDataLayer()`/`AddVideoForensicsServerCore()`.** If a client-side MCP task seems to need one of those calls to work, that's a sign the task is being done wrong, not a sign to add the call back.
- **Mapping naming convention:** DTO mapping extension methods are always `entity.ToDto()` and `dto.ToDomain()` — not `FromDto`, not a mix.

## UI Layout (Desktop-First)

`VideoForensics.Ui.Shared` uses a **desktop-first layout** with resizable panels via Syncfusion's SfSplitter. This is optimized for:
- Web app (full browser)
- MAUI desktop (WinUI on Windows)

**Mobile optimization is pending.** MAUI mobile (iOS/Android) needs a separate, touch-friendly layout with collapsible panels and vertical stacking instead of side-by-side panes. Do not add mobile-specific layout logic to MainLayout—create a new mobile layout component or detect platform and swap layouts at the Routes level.

## Visual Studio MCP (`local-sdk`)

This repo has a live Visual Studio instance reachable via the `local-sdk` MCP server
(`http://localhost:3011/sdk`, registered in `.mcp.json`) with the `VideoForensics` solution
already loaded. **Always use it when available** instead of ad-hoc grep/manual reading for the
things it does natively:
- Symbol navigation: `FindSymbolDefinition`, `FindSymbolUsages`, `GetMethodCallers`, `GetMethodCalls`, `GetSymbolAtLocation`, `GetInheritance` instead of grepping for a class/method by name.
- Solution/project structure: `GetSolutionTree`, `GetProjectReferences`, `GetDocumentOutline` instead of `find`/`Glob` over `.csproj`/`.cs` files.
- Compile errors/warnings: `GetDiagnostics`/`ErrorListGet` as a first check alongside (not instead of) `dotnet build`, since it reflects the IDE's live Roslyn analysis.
- Refactors: `RenameSymbol` and `FormatDocument` for renames/formatting instead of hand-editing every call site.
- Debugging a real repro: `DebugStart`/`DebugAttach`, `BreakpointSet`/`BreakpointList`/`BreakpointRemove`, `DebugContinue`/`DebugStep`, `DebugGetCallstack`/`DebugGetLocals`/`DebugEvaluate` instead of asking the user to describe what happened.
If the server isn't connected (tools not listed / calls fail), fall back to the usual Bash/Grep/Read tools and mention that `local-sdk` was unreachable rather than silently guessing at its state.

## First-Run Admin Setup

On first startup with an empty database, the interactive `/setup` page (`Setup.razor` + `SetupEndpoints.cs`) is the default way to create the initial SuperAdmin account — the installing user picks their own username and password directly (no forced password change needed, since they chose it). `AuthGate.razor` redirects an unauthenticated visitor to `/setup` whenever `IOperatorRepository.IsEmptyAsync()` is true; the endpoint itself re-checks the same condition and returns 403 once any operator exists, so it can't be used to create a second SuperAdmin later.

For headless/scripted deployments that can't drive a browser wizard, the legacy fixed-password seed is still available but off by default: set `VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN=true` before first startup to have `InitializeVideoForensicsDataAsync()` in `VideoForensicsHostingExtensions.cs` auto-create the account with:
- Username: `admin`
- Password: `ChangeMe123!` (the fixed `DefaultSuperAdminPassword` constant)
- Role: `SuperAdmin`
- MustChangePassword: `true` (forces password change on first login)

This constant is synchronized in two places: the `DefaultSuperAdminPassword` field in `VideoForensicsHostingExtensions.cs` and in this document. If the password ever changes, update both locations. The seeding is idempotent — if any operator exists, no default account is created, and it never overrides an admin already created via `/setup`.

## Branching and pull requests

- **`main` only accepts pull requests from `dev`.** All work branches (feature, fix, `claude/*`) open their PRs against `dev`; `main` is updated solely by a `dev` → `main` promotion PR. The `main-source-guard` workflow (`.github/workflows/main-source-guard.yml`) fails any PR into `main` whose head is not this repo's `dev` — it must be a required status check on `main`.
- Never open a PR from a work branch directly into `main`, and never push directly to `dev` or `main`.

## Execution workflow

- **Always delegate implementation work to Haiku subagents.** The main session (Sonnet) plans and designs only — it does not write or edit implementation files directly, even for "just one file" or when already mid-task. Dispatch each file/service change (or a small batch of related files) to a Haiku subagent. Only escalate specific work to Sonnet if a Haiku subagent reports it's blocked or confused (ambiguous existing code, can't locate a call site, etc.) — never preemptively use Sonnet for work that has a clear, prewritten approach.
- **Lite gate:** the default verification step after a Haiku subagent finishes a file/service change, and after each batch of related changes within a plan. Scope is limited to what changed — no solution-wide rebuild, no package updates. Run `dotnet build` (incremental, not clean) on just the touched `.csproj` files, then `dotnet test` on just their sibling `tests/` projects (and any other test project that references the changed code). Fix any build errors/warnings or test failures the touched projects surface before moving on. This does not require asking the user first — it's the normal build+test loop, not a gate on committing.
- **Committing and pushing a branch (no PR yet) only requires the lite gate** to have already passed for every change being committed/pushed — do not run the full gate just to commit or push a branch.
- **Full gate:** required before opening a pull request (not merely before a commit/push). **Before running it, always ask the user for confirmation** (it's slow and touches the whole solution, not just the current change). Once confirmed: first update all outdated NuGet packages solution-wide (`dotnet list VideoForensics.sln package --outdated`, then bump every flagged `PackageReference` to its listed `Latest` version across every `.csproj` that references it — same version for the same package everywhere, never partially bump one project), then do a clean rebuild of the whole solution (`dotnet clean` + `dotnet build`, not an incremental build), fix every warning/error/notice it surfaces (not just ones touching the current change or introduced by the package bump), then run the full test suite (`dotnet test`, not just tests for the current change) and fix any failures. This gate runs after the lite gate has already passed for every change in the plan, and applies to every PR, not just large ones.
