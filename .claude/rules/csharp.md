---
paths:
  - "**/*.cs"
---

# C# conventions

- `.csproj`: `Microsoft.CodeAnalysis` 5.9.0. Signed projects use `VideoForensics.snk` (root).
- Before hand-rolling code, check NuGet for a maintained, secure package, and for a newer or patched version of anything already used. Prefer it.
- Public APIs are interfaces in `Contracts/`. No vendor SDK outside service layers.
- Async I/O. Async methods take `CancellationToken`.
- Log user paths: Info on success, Error on failure, with context. Use account names, never GUIDs. Expose errors via `GetLastError()`.
- Never log stack traces. On failure, log the message and exception type with context, at Error. Don't pass the exception object.
- Validate input at API boundaries. No secrets or plaintext passwords in code.
- Public interfaces get XML docs. Complex logic gets "why" comments.
- Type names that clash across namespaces (e.g. `Device` in Core.Providers.Common.Contracts and Data.Common.Entities): fully qualify or alias in the file. Two `using`s give CS0104.
- Tests: see `tests.md`.
