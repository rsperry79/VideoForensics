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

## Testing

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
