---
name: pr-promotion
description: VideoForensics PR and release rules: work branches target dev, "PR to main" two-step promotion, squash vs merge-commit exceptions. Use when opening, merging, or promoting a PR.
---

# PR and promotion

- `main` accepts PRs only from `dev`, enforced by `.github/workflows/main-source-guard.yml` (must be a required check on `main`).
- Work branches (feature, fix, `claude/*`) open PRs against `dev`.
- Never push directly to `dev` or `main`. Never open a work-branch PR directly into `main`.
- Squash-merge every ordinary PR (work into `dev`, or `dev` into `main`). No merge commits.

## "PR to main" means two steps
1. Work branch into `dev` via PR, squash-merged.
2. `dev` into `main` via promotion PR, squash-merged.

## Exception: `main` into `dev` reconciliation
- Use a real merge commit, never squash. Use when `main` and `dev` diverged and a promotion PR shows real conflicts.
- Squashing discards parent history, so the same conflict returns on every promotion. A two-parent merge fixes it permanently.
- Verify with `git log --format=%P` that both tips are parents.

Before opening the PR, run the `code-workflow` skill's full gate, after confirming with the user.
