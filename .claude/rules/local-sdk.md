---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Visual Studio MCP (`local-sdk`)

Live VS instance with the solution loaded. Use it before grep or manual reading for navigation (`FindSymbolDefinition`, `FindSymbolUsages`, `GetMethodCallers`), structure (`GetSolutionTree`), diagnostics (`GetDiagnostics`, alongside `dotnet build`), and refactors (`RenameSymbol`). Debug tools are in the same server.

Build, test, and run through this server before the shell: `ExecuteCommand` for builds, `ExecuteAsyncTest` for tests, `DebugStart` to run. Use `dotnet` in Bash only if the server is unreachable.

If the server is unreachable, fall back to Bash/Grep/Read and say so.
