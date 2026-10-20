# VideoForensics

Detail: `.claude/rules/` (path-scoped), `.claude/skills/` (procedures), `.claude/project-layout.md` (on demand).

## Code
- `.csproj`: `Microsoft.CodeAnalysis` 5.9.0. Signed projects use `VideoForensics.snk` (root).
- Public APIs are interfaces in `Contracts/`. No vendor SDK outside service layers.
- Non-test changes ship with tests (TDD). Async I/O; async methods take `CancellationToken`.
- Log user paths (Info success, Error failure) with context; account names, never GUIDs. Expose errors via `GetLastError()`.
- Validate input at API boundaries. No secrets or plaintext passwords in code.
- Public interfaces get XML docs; complex logic gets "why" comments.
- Breaking change: update this file and the commit message.

## Workflow
- Code work in a worktree from `wip`, never the main checkout (skill `code-workflow`).
- Never push `dev` or `main` (skill `pr-promotion`).
- Haiku subagents write files; verify their output. Confirm before any full gate.
- Never `git clean -X`. Never kill processes in bulk.
- Log failures to `.claude/task_failure_log.md` (format in `code-workflow`).

## Output
- Terse. No trailing summaries.
- Do not read `docs/` or `archive/` unless asked.
