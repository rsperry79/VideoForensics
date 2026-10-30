# VideoForensics

Detail: `.claude/rules/` (path-scoped, e.g. `csharp.md`), `.claude/skills/` (procedures), `.claude/project-layout.md` (on demand).

- Code work, worktrees, subagents, `git clean`, failure log: skill `code-workflow`.
- Never push `dev` or `main`: skill `pr-promotion`.
- Breaking change: update this file and the commit message.
- Output: terse. No trailing summaries.
- Do not read `docs/` or `archive/` unless asked.
- Before writing backwards-compat code (fallbacks, legacy-path handling, migration shims), ask whether to fix or add the data/code instead.
