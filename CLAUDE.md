# VideoForensics

## STOP: NO NON-HAIKU SUBAGENT WITHOUT THE USER'S YES
- Every subagent is `model: "haiku"`, except the plan review below. No self-approval.
- Plan review (standing): before each Haiku implementer, one read-only Sonnet agent reviews the plan/brief. No per-plan ask.
- Any other model: ask in chat, name the criterion from `~/.claude/docs/subagent-model-selection.md`, wait for a clear yes. Approval covers that one spawn.
- This overrides escalation text elsewhere (global CLAUDE.md, code-workflow skill, subagent doc).
- Breaking this is a defect: say so and log it in `.claude/task_failure_log.md`.

## MANDATORY: read and follow before any work
Before the first tool call of every task, read this file. Before you touch a file, read every `.claude/rules/*.md` that matches it. These override your own judgment. Never skip, reinterpret, or "improve on" them.

Before code work: read `.claude/skills/code-workflow/SKILL.md` (worktrees, subagents, gates, handoff).
Before logging a failure: read `.claude/failure-log.md` (format and review).
Before any subagent spawn: read `~/.claude/docs/subagent-model-selection.md`.

Most-broken rules. Breaking one is a defect: fix it at once and log it.
- All edits, builds and tests happen in a worktree, never the main checkout.
- Failing test first and record the red run. Verify every subagent result yourself.
- Log each failure in `.claude/task_failure_log.md` when it happens. Rerun a failing test on `wip` for a baseline before calling it flaky or unrelated.
- Output terse. No trailing summaries, no recaps of what the user already saw.
- Keep the file's existing line endings (CRLF files stay CRLF).
- Ask before writing backwards-compat code (fallbacks, legacy-path handling, migration shims): ask whether to fix or add the data/code instead. Ask one push question, never two.

If an instruction conflicts with your judgment, follow the instruction or ask. Never deviate silently. If you must deviate, say so in that same message.

Detail: `.claude/rules/` (path-scoped, e.g. `csharp.md`), `.claude/skills/` (procedures), `.claude/project-layout.md` (on demand).

- Never push `dev` or `main`: skill `pr-promotion`.
- Breaking change: update this file and the commit message.
- Do not read `docs/` or `archive/` unless asked.
