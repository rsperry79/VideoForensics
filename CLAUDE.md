# VideoForensics Development Guidelines

Detail lives in `.claude/rules/` (path-scoped, load when matching files are read), `.claude/skills/` (procedures), and `.claude/project-layout.md` (read on demand).

## Core
- Every `.csproj` references `Microsoft.CodeAnalysis` 5.9.0 (match sibling projects).
- Public APIs are interfaces in `Contracts/`. No vendor SDK outside service layers.
- Every non-test change ships with tests for each new path. Write tests first (TDD).
- Async for all I/O; async methods take `CancellationToken`.
- Log user-facing paths (Info on success, Error on failure) with context. Expose errors via `GetLastError()`. Log account names, never raw GUIDs.
- Validate input at API boundaries only. No secrets in code. No plaintext passwords.
- Public interfaces get XML docs. Complex logic gets "why" comments. Breaking changes update this file and the commit message.

## Workflow
- Code work runs in a worktree forked from `wip`, never the main checkout. Procedure: skill `code-workflow`.
- Never push to `dev` or `main`. PR and promotion rules: skill `pr-promotion`.
- Haiku subagents write implementation files; verify their output yourself.
- Confirm with the user before any full gate.
- Never `git clean -X`. Never kill processes in bulk.
- Log each failed task, build, test, or tool call to `.claude/task_failure_log.md` (format in `code-workflow`).

## Output
- Terse. One sentence on what changed. No trailing summaries.

## Off-limits
- Do not read `docs/` or `archive/` unless asked.
