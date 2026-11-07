---
name: ci-release
description: VideoForensics CI, build, test, and release rules. Which workflow runs when, the required test gate before publishing, version and tag handling, secrets, and how to change a workflow. Use when editing .github/workflows, version.json, or deploy files, when a CI run fails, or when a release or publish is involved.
---

# CI, build, test, and release

## Pipeline (`.github/workflows/ci.yml`)
Triggers: push and pull request to `main` and `dev`.

| Stage | Workflow | Runs on | Needs |
|---|---|---|---|
| check-dependencies | check-dependencies.yml | pull request only | none |
| check-js | check-js.yml | all | none |
| build-windows | build-windows.yml | all | none |
| test-windows | test-windows.yml | all | build-windows, check-js |
| determine-version | determine-version.yml | push to `dev`/`main` | test-windows |
| publish-packages | pack-nuget-packages.yml | push to `dev`/`main` | determine-version |
| publish-releases | release-installers.yml | push to `dev`/`main` | test-windows |

Also: `build-ios.yml` (pull request to `main`/`dev`, manual), `codeql.yml`, `main-source-guard.yml` (pull request to `main`: only from `dev`).

## Required rules
1. **Test gate.** Publishing is blocked until test-windows passes. Do not bypass it with `if: always()` or `continue-on-error` on publish jobs.
2. **Same binaries.** build-windows uploads `build-output.tar`. test-windows extracts it and runs `dotnet test --no-build`. Tests must not rebuild, or they test different code than was built.
3. **Publish only from branches.** Publishing jobs check `refs/heads/dev` or `refs/heads/main`. Pull requests never publish.
4. **Version from NBGV.** `version.json` sets the version. The release tag is `v<major>.<minor>-<branch>.<run>` for branches, `v*` for tag releases. Dev creates a `Testing` prerelease, main creates a `Release` release. Do not create or move release tags by hand; the workflow force-moves the `Testing`, `Release`, and `v*-dev`/`v*-release` label tags.
5. **Secrets.** `SYNCFUSION_LICENSE_KEY`, `ANTHROPIC_API_KEY`, `GITHUB_TOKEN` come from `secrets.*`. The live Anthropic test skips when the key is empty, so forks stay green.
6. **Unsigned installers.** Windows and Debian installers are unsigned. SmartScreen warnings are expected until a certificate exists.
7. **Required checks.** `main` requires `only-from-dev` (the `main-source-guard` job) and `test-windows / test`. Reviews are not required and admins are not enforced, so the owner can still promote. Check with `gh api repos/rsperry79/VideoForensics/branches/main/protection`. Changing these is a repo setting: ask the user first.

## Test specifics
- Microsoft.Testing.Platform is on (`Directory.Build.props`). Use MTP's flags: `--report-xunit-trx`, not `--logger trx`.
- Filters and test layout: `.claude/rules/tests.md`.

## Changing a workflow
1. Edit on a worktree branch from `wip`. Keep LF line endings.
2. Check the YAML parses. Read the job's `needs` and `if` conditions after the change.
3. Open a PR to `dev`. Its CI run is the verification. Local builds do not prove a workflow works.
4. Check the run's logs for the changed step before merging.

## When CI fails
- Read the failing job's log for the first error. Do not guess from the job name.
- Log the failure in `.claude/task_failure_log.md` (global `failure-log` skill).
- Rerun a single job or the full workflow only after fixing the cause, or to confirm a flaky test (see the Watch list in the failure log).

## Releasing
- Releases come from merges to `dev` (Testing prerelease) and `main` (Release). Promotion follows `pr-promotion`.
- Before a release PR, run the `full-gate` skill.
