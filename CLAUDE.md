# VideoForensics

## STOP: NO NON-HAIKU SUBAGENT WITHOUT THE USER'S YES
- Subagents are `model: "haiku"`, except the standing Sonnet plan review (code-workflow skill) and a model the user approves in chat (criteria: `~/.claude/docs/subagent-model-selection.md`; approval covers one spawn). This overrides escalation text elsewhere. Breaches: say so and log in `.claude/task_failure_log.md`.

## MANDATORY: read and follow before any work
Before the first tool call of every task, read this file. Before you touch a file, read every `.claude/rules/*.md` that matches it. These override your own judgment. Never skip, reinterpret, or "improve on" them.
Before code work, read `.claude/skills/code-workflow/SKILL.md` (the SOP). Before any subagent spawn, read `~/.claude/docs/subagent-model-selection.md`.
On demand: `.claude/rules/` (path-scoped, e.g. `csharp.md`), `.claude/skills/` (procedures), `.claude/project-layout.md`.

## Invariants. Breaking one is a defect: fix it at once and log it.
- All edits, builds and tests happen in a worktree, never the main checkout.
- Failing test first (record the red run). Verify subagent results. Log failures in `.claude/task_failure_log.md` when they happen. Detail: code-workflow skill.
- Never push `dev` or `main`: skill `pr-promotion`.
- Keep the file's existing line endings (CRLF files stay CRLF).
- Ask before writing backwards-compat code (fallbacks, legacy-path handling, migration shims): ask whether to fix or add the data/code instead. Ask one push question, never two.
- Output terse. No trailing summaries, no recaps of what the user already saw.
- Breaking change: update this file and the commit message.
- Do not read `docs/` or `archive/` unless asked.

If an instruction conflicts with your judgment, follow the instruction or ask. Never deviate silently. If you must deviate, say so in that same message.
