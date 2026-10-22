---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Visual Studio MCP (`local-sdk`)

Live VS instance with the solution loaded. Use it before grep or manual reading:
- Navigation: `FindSymbolDefinition`, `FindSymbolUsages`, `GetMethodCallers`, `GetMethodCalls`, `GetSymbolAtLocation`, `GetInheritance`.
- Structure: `GetSolutionTree`, `GetProjectReferences`, `GetDocumentOutline`.
- Diagnostics: `GetDiagnostics` / `ErrorListGet`, alongside `dotnet build`.
- Refactors: `RenameSymbol`, `FormatDocument`.
- Debugging: `DebugStart`/`DebugAttach`, `BreakpointSet`, `DebugStep`, `DebugGetLocals`, `DebugEvaluate`.

If the server is unreachable, fall back to Bash/Grep/Read and say so.
