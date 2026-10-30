# Bug batch (2026-10-23)

Source: pasted bug list. Each non-test change follows TDD. Work in a worktree from `wip`.

## Order

1. Correctness and false confidence
   - Ring snapshot fallback: a failed fresh snapshot must not silently use the latest image. Mark the result stale or failed in the UI and logs.
   - Flood camera shows online when offline: derive online status from recent heartbeat or health data.
   - Timelapse interval: capture every 15 minutes. Fix the interval setting and the scheduler that reads it.
2. Resilience
   - Ring throttling: catch `ThrottledException` at the service boundary. Show "rate-limited until HH:mm", keep the persisted backoff, stop per-request retries, and keep raw exception text out of the UI.
   - Thread watchdog: monitor background services and worker threads. Log hung or dead loops and restart or alert.
3. Naming
   - Device labels: show human-readable name plus short ID suffix wherever a device appears. Snapshot messages use the same label.
4. Auth
   - Passkey over LAN HTTP: WebAuthn needs a secure context. Choose HTTPS for LAN, a clear "passkey unavailable here" message, or an alternate sign-in.
5. Data
   - Log bloat: measure DB size and log row counts, add retention, move logs out of the main DB.
6. New page
   - SNR page: per-camera SNR timeline and summary stats. Check whether SNR is already recorded first.
7. Installer
   - Remove bundled ffmpeg and cloudflared from `deploy/windows/VideoForensics.iss`.
   - Fix optional installs so remaining components are selectable.
8. API tester
   - Clickable rows open a sub-report.
   - Remove the invitations API.
   - Run the get-ID call before any call that needs the ID.
9. UI
   - Move the "Signalling" ticket to the alarm view.
   - Right sidebar must not show details for items selected in the center pane.
   - Theme switch in the user menu.

## Decisions

- Item 5 (log bloat): decided by the user: use a local SQLite `logs.db` with a retention job. Cosmos DB and Table Storage were considered and not chosen.
- Item 9 "Signalling to alarm": not camera-related, and the user cannot test it on hardware. Use fakes for any hardware-dependent test.

## Blocker

`wip` does not contain `origin/dev` (merge-base check fails). A test merge into the main checkout was refused: 888 uncommitted files overlap origin/dev files, and some untracked files would be overwritten. Stash or commit is needed first; the working tree was left unchanged.
