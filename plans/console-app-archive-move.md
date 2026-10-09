# Console app archive

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. `src/client/VideoForensics` exists on origin/dev (8 files: `VideoForensics.csproj` plus 7 `.cs` files). No `archive/` directory exists outside `plans/archive/`.

**Source**: `plans/archive/maui-blazor-hybrid-conversion.md` line 8 (M4 status: console app "kept in place", user chose "hold off") and line 381 (M4 milestone: move `src/client/VideoForensics` to `archive/VideoForensics` once parity is reached). Also lines 39, 53 and 444 of the same file, and `plans/archive/frontends-mcp-api-only-architecture.md` line 63 (console app out of scope, "slated to be archived soon").

## Verdict

STILL OPEN, deliberately deferred. The real path on origin/dev is `src/client/VideoForensics` (singular `client`), which matches the item. CLAUDE.md's project-structure section says `src/clients/VideoForensics` (plural), and so do stale references in `src/client/VideoForensics.Mcp/README.md` and `_docs_external/E2E_TESTING_GUIDE.md`. Those are doc errors, not a second directory. The target `archive/VideoForensics` is only named in the old plan, and CLAUDE.md says no `archive/` dir exists, so the target path needs a decision.

## Parity gate

The parity criteria are NOT written down in a testable form. The old plan only says "once M4 reaches parity" (line 381) and "once the new app reaches parity" (line 39). The M1-M4 status lines (line 8) say M1-M4 are done, yet the user explicitly chose to hold off, so the gate is a user decision, not a technical one. Proposed gate to confirm with the user before moving:
- Every console screen (~30, per plan line 28) has a MAUI/WebApp equivalent, checked against a written screen-by-screen matrix (does not exist yet; must be created).
- Console-only features have a home: server-tier health sync and RSSI, backups, and the Full Forensic Workflow wizard.
- Remaining M5-M6 gaps (`[~]` in the status block) do not block anything the console currently does.
- The user gives an explicit go-ahead in chat.

## Scope

In: move the console project out of the active tree, update every reference, keep CI green.
Out: deleting it, migrating features, anything in `docs/`, archiving `src/client/VideoForensics.Mcp` or `src/client/VideoForensics.Client.Core.tests`.
Decision needed on target: (a) a literal top-level `archive/VideoForensics`, excluded from the sln and CI (matches the old plan, but then CLAUDE.md's "no archive/" notes flip), or (b) `git rm` and rely on git history. Recommend (a), and keep it out of the build.

## Code and file touchpoints

All verified on origin/dev.

| File | Change | Why |
|---|---|---|
| `src/client/VideoForensics/*` (8 files) | `git mv` to `archive/VideoForensics/` | The move itself; preserves history |
| `VideoForensics.sln` (line 139, project `{5399C3E3-A558-46FB-A82F-380429A2AA00}`) | Remove the project entry, its config/platform lines and any nesting entries | The sln must not point at a missing path |
| `VideoForensics.CI.slnf` (line 12) | Remove `src/client/VideoForensics/VideoForensics.csproj` | CI filter would fail to load the missing project |
| `archive/VideoForensics/VideoForensics.csproj` | Optionally add an `archive/Directory.Build.props` that blocks building (or leave unreferenced); its relative `..\` ProjectReferences become stale | Archived code is not expected to compile |
| `CLAUDE.md` (line 17 project structure; "no archive/" notes) | Fix `clients/` to `client/`, drop or move the console entry, update the archive notes | Doc is currently wrong on the path and will be wrong on the archive |
| `plans/archive/api-error-logging-per-attempt.md` (lines 11, 53) | Likely leave: it is a historical plan. The `dotnet ef --startup-project src/client/VideoForensics` command would break; the EF startup project should be re-pointed to the WebApp (separate check) | Active EF tooling should not depend on the archived project |
| `src/client/VideoForensics.Mcp/README.md` (lines 10, 16, 49), `_docs_external/E2E_TESTING_GUIDE.md` (lines 16, 830) | Replace `src/clients/VideoForensics` run instructions with the WebApp or archived path | Stale plural path and run commands |

No `.github/workflows/*.yml` references `src/client/VideoForensics`: build-ios.yml and release-installers.yml reference only MauiApp and WebApp. No `.csproj` on origin/dev has a ProjectReference to the console project (grep found none). `deploy/` and the installer pipeline need a check for a `VideoForensics.exe` dependency (not found in grep, but verify).

## Test plan

- `dotnet sln VideoForensics.sln list` and `dotnet build` on the touched sln and the CI slnf: both load with no missing-project errors.
- Build `VideoForensics.Client.Core.tests` and `VideoForensics.Mcp` (siblings that the console shared Client.Core with) and run `dotnet test --filter` on `VideoForensics.Client.Core.tests`.
- Confirm the CI workflows (`ci.yml`, release-installers, build-ios) restore and build on a PR to `dev`.
- `git grep -n "client/VideoForensics\b\|clients/VideoForensics"` returns only intended historical hits.
- The console project has no tests of its own, so no new tests are required for this pure move.

## Risks and open questions

- Parity is undefined and the user previously held off: do not execute without an explicit go.
- Archive target: top-level `archive/` or removal; CLAUDE.md must be updated either way.
- The console is the only host that runs without the WebApp; archiving removes the headless/scripted path (check the `VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN` and deployment docs).
- EF migration tooling currently uses the console as startup project in one old plan; confirm the current tooling.
- The sln GUID/nesting cleanup can leave orphaned configuration lines if hand-edited; use `dotnet sln remove`.

## Priority recommendation

Low: cosmetic repo hygiene, blocked on a user decision and an undefined parity gate, with no functional impact. Effort S (about 1-2 hours once the gate is approved; the parity matrix itself is M).

## Implementation dispatch

One Haiku 5.5 subagent (per the memory note) on a new branch off `dev`, after the user approves the parity gate and the archive target; verify with the lite gate.
