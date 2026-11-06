# VideoForensics

## STOP: NO NON-HAIKU SUBAGENT WITHOUT THE USER'S YES
- Subagents are `model: "haiku"`. Exceptions: the standing Sonnet plan review in the code-workflow skill, and any model the user approves in chat (name the criterion from `~/.claude/docs/subagent-model-selection.md`; approval covers that one spawn). No self-approval.
- This overrides escalation text elsewhere (global CLAUDE.md, code-workflow skill, subagent doc).
- Breaking this is a defect: say so and log it in `.claude/task_failure_log.md`.

## MANDATORY: read and follow before any work
Before the first tool call of every task, read this file. Before you touch a file, read every `.claude/rules/*.md` that matches it. These override your own judgment. Never skip, reinterpret, or "improve on" them.

Before code work: read `.claude/skills/code-workflow/SKILL.md` (the SOP: worktrees, subagents, plan review, test first, gates, failure log, handoff).
Before any subagent spawn: read `~/.claude/docs/subagent-model-selection.md`.

## Invariants. Breaking one is a defect: fix it at once and log it.
- All edits, builds and tests happen in a worktree, never the main checkout.
- Failing test first; record the red run. Verify every subagent result yourself. Log each failure in `.claude/task_failure_log.md` when it happens. Detail: code-workflow skill.
- Never push `dev` or `main`: skill `pr-promotion`.
- Keep the file's existing line endings (CRLF files stay CRLF).
- Ask before writing backwards-compat code (fallbacks, legacy-path handling, migration shims): ask whether to fix or add the data/code instead. Ask one push question, never two.
- Output terse. No trailing summaries, no recaps of what the user already saw.
- Breaking change: update this file and the commit message.
- Do not read `docs/` or `archive/` unless asked.

If an instruction conflicts with your judgment, follow the instruction or ask. Never deviate silently. If you must deviate, say so in that same message.

Detail: `.claude/rules/` (path-scoped, e.g. `csharp.md`), `.claude/skills/` (procedures), `.claude/project-layout.md` (on demand).
