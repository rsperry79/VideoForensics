# Simple Mode browser check

**Status on origin/dev (checked 2026-10-09):** STILL OPEN (manual verification; no code needed unless the check finds defects)

**Source:** plans/archive/simple-mode-embedded-mcp-chat.md line 13 ("Not verified in a browser.")

## Verdict

STILL OPEN. Simple Mode shipped in dd181e4 (#155), 45b0d81 (#183, lock/toggle/admin control/plain-language home) and 89a8533 (#188, localization), but no commit or test on origin/dev drives it in a real browser (only bUnit/xUnit tests exist: SimpleModeLayoutTests, SimpleHomeTests, SimpleLayoutHomeRoutingTests, OperatorDetailsDisplayModeTests, UiModeServiceTests). This is a manual verification pass using the existing `webapp` launch config; defects found become separate fix items.

## Scope

Checked symbols/files on origin/dev: `SimpleLayout.razor`, `SimpleHome.razor`, `ResponsiveLayout.razor`, `SimpleHomeBuilder`, `UiModeService`, `UserMenuButton.razor`, `OperatorDetails.razor`, `ChatPanel.razor`, `.claude/launch.json`, `SetupEndpoints.cs`.

Changes since the archived plan:
- The archived limitation "plain Admin sees an unauthorized alert on Operator details" is fixed by local-only commit 67d7bf6 (branch feature/operator-details-admin, NOT on origin/dev). Until it merges, step 7 will show the alert for an Admin viewer; that is expected, not a new bug. After it merges, the Admin step must show no alert.
- Streaming chat is no longer deferred (#189, #192); verify chat streams.
- Simple home appears only at `/` and `/evidence` (`SimpleLayout.IsHomeRoute`); all other routes render `@Body` inside the Simple frame.

Launch: `.claude/launch.json` config `webapp` runs `dotnet run --project src/client/web/VideoForensics.WebApp/VideoForensics.WebApp.csproj --urls http://localhost:5162` (use `preview_start name=webapp`). Fresh DB: first visit redirects to `/setup` (create SuperAdmin). A second ReadOnly operator is needed for the victim view (create via `/settings/operators`; sign in on a second browser profile/tab via `/device-signin`). Chat needs an LLM key at `/settings/llm`; without one, verify only the friendly error path.

### Verification script

| # | Route / action | Expected |
|---|---|---|
| 1 | `/` as SuperAdmin (Standard mode) | Standard MainLayout with nav; no Simple frame. |
| 2 | User menu > switch to Simple | Layout swaps without reload to Simple frame: header "VideoForensics", "Standard view" and "Sign out" buttons, ChatPanel on the right; `/` shows `SimpleHome` heading, 7-day timeline (or "no activity" text), evidence section with Open links. |
| 3 | Reload `/` and `/evidence` | Mode persists (server-side preference); both routes show SimpleHome; no raw event type strings, no English key names (`L[...]` keys not leaking). |
| 4 | `/cases`, `/chat`, `/change-password` while Simple | Render as routed content inside the Simple frame (not replaced by SimpleHome). |
| 5 | "Standard view" button | Returns to MainLayout; user menu toggle back works. |
| 6 | Operator details (`/settings/operators/{id}`) as SuperAdmin, victim operator: set Display mode = Simple, check Lock, Save | "Saved" message; audit log shows `OperatorUiModeChanged`. |
| 7 | Same page as plain Admin | Display mode section works; unauthorized alert present only until 67d7bf6 merges. |
| 8 | Sign in as the locked victim | Lands in Simple; no "Standard view" button; user menu has no mode toggle; no console errors. |
| 9 | Victim: ask chat "what happened to my cameras this week?" | Reply streams incrementally; no tool JSON or GUIDs displayed; with no LLM key, a friendly error, not a stack trace. |
| 10 | Evidence "Open" link | Opens media in new tab (`rel=noopener`); unavailable items show the "unavailable" text. |
| 11 | Victim hits `/settings/operators` directly | Role gate blocks (ReadOnly); no data leak. |
| 12 | Resize to 375px wide while Simple | Simple frame still used (mode check precedes `IsMobile` in `ResponsiveLayout`); note whether content/chat stack usably. Record as finding only. |
| 13 | Console + network (read_console_messages, read_network_requests) | No 4xx/5xx on `/api/v1/...` beyond expected 403s in step 11; no unhandled Blazor circuit errors; Simple home triggers one repository load, not two (loads in `OnAfterRenderAsync`). |

Capture screenshots for steps 2, 8, 9 and attach to the PR or a short results note.

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| (none) | No code changes planned | Pure verification. |
| `.claude/launch.json` | None (verified present, config `webapp`, port 5162) | Used to start the app. |
| `plans/simple-mode-browser-check-results.md` (NEW, optional) | Record pass/fail per step | Only if defects are found. |

## Test plan

No new automated tests for the check itself. If a step fails, the fix follows TDD: add a failing test first in `src/client/ui/VideoForensics.Ui.Shared.Tests` (e.g. `SimpleLayoutHomeRoutingTests`, `SimpleHomeTests`, `UserMenuTests`; naming `<Class>_<Scenario>_<Expected>()`), confirm it fails for the observed reason, then fix. Run only scoped `dotnet test --filter` for the touched classes.

## Risks and open questions

- Needs a populated DB (events, media) or the timeline is just the empty state; seed via a Ring/Wyze sync or import to exercise step 2/10.
- Chat step needs a real LLM key; do not commit or paste it (enter via `/settings/llm` only).
- Order dependency: run step 7 after 67d7bf6 lands or note the expected alert.
- MAUI remote-client Simple home limitation (no jamming repository) is out of scope; web only.
- Do not delete ProgramData/AppData dirs to get a fresh DB without checking contents first.

## Priority recommendation

Medium: Simple Mode is the victim-facing surface and has only unit-test coverage, but exposure is low (display preference only, auth unchanged) and effort is small.
Effort: S (about one hour manual).

## Implementation dispatch

No code work. Run the script with the Browser pane tools (preview_start `webapp`); dispatch a Sonnet subagent only for fixes that the check surfaces, one per defect, tests first.
