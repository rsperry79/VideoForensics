# Provider details, sync, and Query API rework

Status: complete. Items 1-9 are done and merged to `wip` (`5f6c6abb`, `eed2e32a`). Only the follow-ups below remain.

## Done

| Item | What | Commits (wip) |
|---|---|---|
| 1 Account details, Remove on Details tab | Remove Account on the Details tab. Account card shows name, linked, active, last error. Address/name fields from the DB are **not** yet verified against `AccountDtos.cs`. | `7899eed7` |
| 2 Tab bar and subpages | Details / Sync / Cases tabs, route-backed (`/accounts/{Id}`, `/sync`, `/cases`, `/devices`). `/devices/config` retired. | `7899eed7`, `ef61b5f5` |
| 3 Sync subpage | Sync tab: Last Successful Auth, Last Download, Last Sync Completed, controls, output, history, schedule section. `/accounts/{Id}/schedule` retired (schedule embedded in Sync tab). | `7899eed7`, `2bcf3d60` |
| 4 Per-device uptime | Devices tab: Online / Offline / Unknown, last seen, offline threshold setting (default 300 s, editable on the tab). Uses the `data/common` health entity. | `fe8a9d00`, `ef61b5f5` |
| 5 Last Download / initial sync | Sync never writes `LastDownloadTimeUtc` (the legacy video watermark). Sync writes a new `LastSyncCompletedUtc` field (migration). Last Download still shows the legacy value. | `514b4ae3` |
| 6 Validate on Sync tab | "Validate locally" (integrity) and "Validate remote" (provider reconciliation), scoped to the account's devices. `/analyze/validate/*` routes and the nav entry removed. | `fd7f5e94`, `3049e8c5` |

Also done, outside this plan: snapshot interval in seconds with a seconds/minutes selector and a 1 s floor; snapshot pulls, roll-up loops and the Devices tab's latest snapshot/loop (see `snapshot-loop-mp4.md`).

## Open

### 7. Query page (`/query`): partly done
- Done: common filters are one `QueryApiFilterBar` component, used by `QueryApiDevices` (location) and `QueryApiEvents` (location and device). Commit `5f6c6abb` on `feat/query-page-merge` (worktree `F:\VideoForensics-query-merge`).
- Done (`eed2e32a` on `feat/query-page-merge`): raw account status payload. `IProviderAuthService.GetAuthStatusDetails()` returns `ProviderAuthStatus` (authenticated, token type, expiry, scope). No tokens. Ring implements it; other providers return null and the page says so.
- Found while checking: `RemoteProviderAuthService.GetAuthStatus()` throws `NotSupportedException`, so "Check status" fails on the MAUI client. Pre-existing; not fixed here.

### 8. API tester output: done
- Result grid is still a table. Selecting a row shows that call's schema issues in a bottom pane (`call-issues`) and its HTTP calls below it.
- Inspector no longer fed by this grid (`ForensicGrid` without `ToInspector`).

### 9. Merge Query and tester into one page: done, access stricter than planned
- The tester is embedded in `/query`. `/query/api-tester` and `/tools/ring-selftest` redirect there (`Pages/Redirects/ApiTesterRedirect.razor`). Nav entry removed.
- `/query/api-tester` and `/tools/ring-selftest` had no access rule before; the tester now sits behind the `/query` row.
- Access: `/query` is `Admin`, as planned. The tester's destructive and physical endpoints keep their own `SuperAdminLocal` check.
- Server-side check: no `/api/v1/query` endpoints exist in WebApp (`grep` over `src/client/web`). The Query pages call provider services directly, so the 403 conflict does not arise.

## Decisions (resolved)

1. Remove Account stays on the Details tab.
2. `/devices/config` retired. Its per-device config snapshot history has no UI now. `RingDeviceSettings` (`/devices/config/ring/{id}`) lost its entry point; its back link goes to `/accounts`. A link from the Devices tab is a follow-up.
3. Offline threshold: configurable, default 5 minutes (stored in seconds).
4. Query endpoints stay SuperAdmin on the server. Do not change server policy.
5. Item 5 Last Download: separate `LastSyncCompletedUtc` field. The legacy watermark is untouched.
6. Validate routes removed (not redirected).
7. Item 1 rest resolved: `ProviderAccountDto` carries no address or name fields, and none are needed. The account's name is the provider login, read from `User.ProviderUserKey` through `UserRepository` (`AccountTabPageBase.cs:66`). Street address is per location, in `LocationMetadata.StreetAddress`, and the Details tab does not show it. No contract change to `src/core/api`.

## Next

Follow-ups (not part of this plan's scope):
1. `RemoteProviderAuthService.GetAuthStatus()` and `GetAuthStatusDetails()` throw on the MAUI client, so "Check Status" fails there.
2. Wyze and Uniview report no status details. Only Ring implements `GetAuthStatusDetails()`.

Each step ships with tests (TDD). Breaking changes update `CLAUDE.md` and the commit message.
