---
name: code-workflow
description: VideoForensics code-work procedure: worktrees forked from wip, lite and full gates, batch handoff to wip, failure log. Use when starting, building, or handing off a plan or feature batch.
---

# Code workflow

## Branches
- `dev` = integration. `wip` = local dev base; all code work starts from `wip`. `main` = release, only moves by merging `dev`.
- Before code work or a PR from a `wip` branch: `git fetch origin`, then `git merge-base --is-ancestor origin/dev wip`. If it fails, `git merge origin/dev` into `wip`. Recheck after any PR lands in `dev`.
- Match worktrees by `refs/heads/<branch>` from `git worktree list --porcelain` (strip `\r`), never by folder name.

## Worktrees
- If this chat already has a worktree (created earlier, or the session's cwd is one), reuse it. Confirm it in `git worktree list --porcelain` and continue there. Do not create a second one.
- Otherwise create per feature: `git worktree add <sibling-path>/<feature> -b <feature-branch> wip`. Name it for the feature, not the session.
- All edits, commits, and builds happen in the worktree. Leave the main checkout's uncommitted changes alone.
- Before removing a worktree, confirm its path in `git worktree list --porcelain`.

## Implementation
- Haiku 5.5 subagents (`model: "haiku"`) write implementation files, one file or small related batch each. Verify their output yourself: build, test, read the diff.
- Blocked or confused subagent: resolve the ambiguity in the brief and re-dispatch.
- Briefs for a Ui.Shared page must name the localization rule (`rules/ui-shared.md`) and list the page's existing literals to convert.
- Escalation rules: `~/.claude/docs/subagent-model-selection.md`.

## Lite gate (after each file/service change or batch)
- `dotnet build` on touched `.csproj` files (incremental).
- `dotnet test --filter` on their tests, plus tests of direct callers.
- Fix failures before moving on. No package updates, no solution-wide build.
- A passing lite gate is enough to commit or push a branch.

## Batch handoff (automatic once the lite gate passes)
1. Commit the batch on its worktree branch.
2. Confirm `wip` contains `origin/dev`, merging first if not.
3. Merge the batch branch into `wip` in the main checkout.
4. `git push origin wip`. Do not push the work branch unless asked.
5. Start the next batch in a new worktree from updated `wip`.
- Stop and report instead when: a gate fails, merges conflict, the main checkout has unrelated changes, or a product decision is needed.

## Full gate (before opening a PR to `dev` or `main`)
- Ask the user to confirm first. Then:
1. Ensure `wip` contains `dev`; sync the work branch with `wip`.
2. `dotnet list VideoForensics.sln package --outdated`. Bump each flagged package to `Latest` in every `.csproj` that uses it, same version everywhere.
3. `dotnet clean` then `dotnet build`. Fix every warning, error, and notice.
4. `dotnet test` the full suite. Fix failures.
5. `npm ci` then `npm run check`. Fix every error.

## Cleanup rules
- Never `git clean -X`. Delete build output by explicit path.
- Never kill processes in bulk. Report hung test hosts instead.

## Failure log
- Log every failure as specified in `.claude/failure-log.md` (format, review, upkeep). Read it when logging or reviewing.
