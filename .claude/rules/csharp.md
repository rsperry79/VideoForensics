---
paths:
  - "**/*.cs"
---

# C# conventions

- `.csproj`: `Microsoft.CodeAnalysis` 5.9.0. Signed projects use `VideoForensics.snk` (root).
- Public APIs are interfaces in `Contracts/`. No vendor SDK outside service layers.
- Async I/O. Async methods take `CancellationToken`.
- Log user paths: Info on success, Error on failure, with context. Use account names, never GUIDs. Expose errors via `GetLastError()`.
- Never log a stack trace. On failure, log the error message and exception type with context, at Error. Do not pass the exception object to the logger.
- Validate input at API boundaries. No secrets or plaintext passwords in code.
- Public interfaces get XML docs. Complex logic gets "why" comments.
- Tests: see `tests.md`.
