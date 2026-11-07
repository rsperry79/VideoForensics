---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Visual Studio MCP (`local-sdk`)

Live VS instance with the solution loaded. Its port is `port` in `.mcp/mcpserver.settings.json` at the repo root (git-ignored, per machine, one per solution). Read it there; never guess the port. Use it before grep or manual reading for navigation (`FindSymbolDefinition`, `FindSymbolUsages`, `GetMethodCallers`), structure (`GetSolutionTree`), diagnostics (`GetDiagnostics`, alongside `dotnet build`), and refactors (`RenameSymbol`). Debug tools are in the same server.

Build, test, and run through this server before the shell: `ExecuteCommand` for builds, `ExecuteAsyncTest` for tests, `DebugStart` to run. If the server is unreachable, fall back to PowerShell 7 (`pwsh`, see the global `powershell` skill), not Bash, and say so. Use `dotnet` from pwsh.
