# Failure log

Format and review for `.claude/task_failure_log.md`. Read this only when logging or reviewing failures.

## Logging
- On any failed task, subagent, build, test, tool, or connector call: append `date | phase | what failed | cause | fix` at the time.
- When a background command fails, read its output file for the actual error before naming a cause.
- Before attributing a test failure to your change, rerun the same filter on `wip` and record the baseline result.

## Phase review
- At phase end, review this phase's entries. For each recurring or avoidable cause, draft one single-sentence rule and ask the user whether to add it. Add only on yes. Do not hold the handoff for the answer.

## Upkeep
- Keep open entries at about 20 or fewer. Delete resolved entries. Mark entries covered by a rule `(covered)`. Summarize the oldest into one line each if over.
