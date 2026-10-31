# VideoForensics

## MANDATORY: read and follow before any work
Before the first tool call of every task, read these in full and follow them. They override your own judgment. Never skip, reinterpret, or "improve on" them.
1. This file.
2. `.claude/skills/code-workflow/SKILL.md` (worktrees, subagents, gates, handoff).
3. `~/.claude/docs/subagent-model-selection.md`, before spawning any subagent.
4. `.claude/failure-log.md`, and every `.claude/rules/*.md` that matches the files you touch.

Most-broken rules. Breaking one is a defect: fix it at once and log it.
- Subagents: `model: "haiku"` by default. Sonnet only when the doc's escalation criteria apply; name the criterion in one line before spawning.
- All edits, builds and tests happen in a worktree, never the main checkout.
- Failing test first and record the red run. Verify every subagent result yourself.
- Log each failure in `.claude/task_failure_log.md` when it happens. Rerun a failing test on `wip` for a baseline before calling it flaky or unrelated.
- Output terse. No trailing summaries, no recaps of what the user already saw.
- Keep the file's existing line endings (CRLF files stay CRLF).
- Ask before writing backwards-compat code. Ask one push question, never two.

If an instruction conflicts with your judgment, follow the instruction or ask. Never deviate silently. If you must deviate, say so in that same message.

Detail: `.claude/rules/` (path-scoped, e.g. `csharp.md`), `.claude/skills/` (procedures), `.claude/project-layout.md` (on demand).

- Code work, worktrees, subagents, `git clean`, failure log: skill `code-workflow`.
- Never push `dev` or `main`: skill `pr-promotion`.
- Breaking change: update this file and the commit message.
- Output: terse. No trailing summaries.
- Do not read `docs/` or `archive/` unless asked.
- Before writing backwards-compat code (fallbacks, legacy-path handling, migration shims), ask whether to fix or add the data/code instead.
