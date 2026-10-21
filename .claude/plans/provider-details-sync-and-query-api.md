# Provider details, sync, and Query API rework

Status: items 1-6 and 9's account-scope work done; items 7, 8 and the Query/tester merge still open. Work branch: `wip` (merged from worktree `last-download-sync-fix`).

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

### 1 (rest). Account details fields
- Street address and other stored account fields: confirm whether `AccountDtos.cs` carries them. If not, adding them is a contract change to `src/core/api`.

### 7. Query page (`/query`)
- Raw account status output alongside the formatted status (collapsible raw JSON).
- Common filters through `QueryApiFilterState` (currently LocationId and DeviceId only), as one filter bar shared with `QueryApiDevices` and `QueryApiEvents`.

### 8. API tester output
- `RingSelfTest.razor` (the tester) becomes a table. Clicking a row shows its issues in a bottom detail pane.
- Remove the issue detail from the right-hand inspector on this page.

### 9. Merge Query and tester into one page, admin+
- Merge `/query` with `RingSelfTest.razor`. Retire the tester route (redirect to `/query`).
- Access: the merged page is `Admin`. The tester route currently has **no** access rule, so any signed-in operator can open it. Close that gap as part of this item.
- Server-side: the earlier plan said to verify whether `/api/v1/query` endpoints exist before deciding on the 403 conflict. **Not yet verified.** A repo search found no such endpoints in WebApp, and the access comment says Query API calls go to provider services directly. If confirmed, the conflict with "server stays SuperAdmin" does not arise.
- Update references: `Layout/NavGroups.cs` (tester entry), and tests `NavGroupsTests.cs`, `QueryApiPageTests.cs`, `RingSelfTestPageTests.cs`.

## Decisions (resolved)

1. Remove Account stays on the Details tab.
2. `/devices/config` retired. Its per-device config snapshot history has no UI now. `RingDeviceSettings` (`/devices/config/ring/{id}`) lost its entry point; its back link goes to `/accounts`. A link from the Devices tab is a follow-up.
3. Offline threshold: configurable, default 5 minutes (stored in seconds).
4. Query endpoints stay SuperAdmin on the server. Do not change server policy.
5. Item 5 Last Download: separate `LastSyncCompletedUtc` field. The legacy watermark is untouched.
6. Validate routes removed (not redirected).

## Next

1. Verify `/api/v1/query` endpoints (decides item 9's conflict).
2. Item 7 (Query page raw status and filters).
3. Item 8 (tester table and detail pane).
4. Item 9 (merge, admin access, tester access rule).
5. Confirm account address/name fields (item 1 rest).

Each step ships with tests (TDD). Breaking changes update `CLAUDE.md` and the commit message.
