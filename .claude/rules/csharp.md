---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# C# conventions

- `.csproj`: `Microsoft.CodeAnalysis` 5.9.0. Signed projects use `VideoForensics.snk` (root).
- Public APIs are interfaces in `Contracts/`. No vendor SDK outside service layers.
- Async I/O. Async methods take `CancellationToken`.
- Log user paths: Info on success, Error on failure, with context. Use account names, never GUIDs. Expose errors via `GetLastError()`.
- Never log stack traces. On failure, log the message and exception type with context, at Error. Don't pass the exception object.
- Validate input at API boundaries. No secrets or plaintext passwords in code.
- Public interfaces get XML docs. Complex logic gets "why" comments.
- Type names that clash across namespaces (e.g. `Device` in Core.Providers.Common.Contracts and Data.Common.Entities): fully qualify or alias in the file. Two `using`s give CS0104.

## Visual Studio MCP (`local-sdk`)

- Live VS instance with the solution loaded. Its port is `port` in `.mcp/mcpserver.settings.json` at the repo root (git-ignored, per machine, one per solution). Read it there; never guess the port.
- Use it before grep or manual reading for navigation (`FindSymbolDefinition`, `FindSymbolUsages`, `GetMethodCallers`), structure (`GetSolutionTree`), diagnostics (`GetDiagnostics`, alongside `dotnet build`), and refactors (`RenameSymbol`). Debug tools are in the same server.
- Build, test, and run through this server before the shell: `ExecuteCommand` for builds, `ExecuteAsyncTest` for tests, `DebugStart` to run. If the server is unreachable, fall back to `pwsh`, and say so. Use `dotnet` from pwsh.
