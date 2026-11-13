# To-do

## Blocked
- [ ] **Calibrate media-analysis thresholds on real footage.** Blocked: no downloaded clips exist yet, so nothing can be checked against real Ring footage. Needs downloads (provider calls) to be approved first. Covers: brightness (`DarkLumaCeiling`), saturation (`GrayscaleSaturationCeiling`), clipping (`FullScaleToleranceDb`, `ClippedPeakThreshold`), noise rise (`NoiseFloorRiseThresholdDb`), dropouts (`DropoutGapThreshold`, `DropoutSilenceRiseFraction`), bitrate collapse (`SuspectedFraction`), minimum baseline size (`MinimumBucketSamples`), and the history window (`HistoryWindowClips`).
- [ ] **HDR brightness.** Blocked: needs real HDR clips. Check that true HDR footage is not misclassified as dark or day after the 8-bit conversion.
- [ ] **Confirm `silencedetect` and `astats` output on a real Ring clip.** Blocked: needs downloaded Ring clips with audio (AAC in MP4).
- [ ] **Initial sync on a new account, end to end.** Blocked: needs a real Ring sign-in through the add-account wizard, which only you can do.

## Open
- [ ] **Devices offline detection.** Apply the flood-camera fix to every device: a device with no recent heartbeat or health data shows offline. See `bug-batch-2026-10.md` item 1.
- [ ] **Live view.** Verify the camera-filter tiles on the live site, confirm remote MAUI clients can resolve `IMediaItemRepository` for the latest still, and decide the one-hour still window. See `bug-batch-2026-10.md` item 10.
- [ ] **Nov 1: confirm the `health.battery_temp` unit.** Stored as deci-Kelvin (`2921` = 18.95 °C = 66 °F) because a wired Front Door doorbell reading 24 °F above a 42 °F evening (98223, 2026-10-30 02:56 UTC, ambient 5.6 °C) is plausible; hundredths of °C (29.21 °C) would be 42 °F above ambient. Pull the stored readings for Oct 31 and Nov 1 and compare with the hourly ambient temperature (Open-Meteo, lat 48.20, lon -122.13). Deci-Kelvin moves about 10 raw units per °C, hundredths of °C about 100. If the slope says hundredths of °C, change the conversion. `status_time` is settled: ticks of a 32768 Hz clock since the Unix epoch.
- [ ] **Battery temp unit: read the sampler output.** The 12-hour sampler writes `scratchpad/battery_temp_samples.csv` (ends about 19:00 UTC on 2026-11-01). Fit the raw-to-ambient slope; close the item in `db-field-coverage.md` item 7 when settled.
- [ ] **Remove `F:\VF-strip`.** Empty leftover worktree folder; the safety check blocked the removal, so you delete it.
- [ ] **Paid-feature equivalence.** 31 backend `paid_features` values were dropped in favour of `RingDevicePaidFeatures` (`CvPaid*`/`OtherPaid*`). Confirm they agree on a device that reports both (none in the live DB yet).
- [x] **Backfill MediaItems codec, resolution and frame rate.** Built: `MediaCodecBackfillService` probes 50 rows per 15-minute pass (about 46 hours for the 9,170 rows). Merged to `wip` at `ef3fdca2`.
- [ ] **Deploy the backfill build.** The running service does not have it yet. Run `src/client/web/VideoForensics.WebApp/scripts/Deploy-WebAppToProd.ps1` from an elevated PowerShell, then check `MediaItems.VideoCodec` fills over time.
- [ ] **Chime and stickup detail fields.** `Devices.ChimeVolume`, `BatteryLife2`, `SirenSecondsRemaining` are mapped but null on this account. Verify once a chime or stickup camera is present.
- [ ] **Test flakes.** `FolderPicker_Cancel_InvokesOnClosedWithNull` (timing); `PlaybackTimeline_MarkerClick_RaisesOnMarkerClick` (pasted failure, not reproduced); `CaseRepositoryNumberingTests` concurrent create (failed once, passes on `wip`).
- [ ] **`RingLocationModeNotYetParticipating.ValueJson`.** Kept as raw JSON until the element shape is known (0 rows live).
- [ ] Step-up checks for account removal and two-factor override: needs an operator with a passkey to test end to end.
- [ ] Ring event retention: undefined. The integration has no retention setting, so decide what to record.
- [ ] Light and night modes: check whether Ring reports the current day or night state; the integration only exposes night-mode and floodlight controls.
- [x] Hemisphere is a configured setting (`MediaAnalysis:Hemisphere`, default northern).
- [x] Drift is recorded on each device (`BaselineDriftDetected`, `BaselineDriftChangeFraction`) for review.
- [x] Analysis is awaited before the next provider call; the shared data client does this for every provider that records downloads (today: Ring).

- [ ] **Flagged media: audio and lighting flags.** Not written yet; they wait for their own report.
- [ ] **Flagged media: timeline flag list.** The Syncfusion flag list on the playback timeline is not built (optional until decided).
- [ ] **Flagged media: legal hold on Keep.** Keep cannot place a legal hold yet.
- [ ] **Flagged media: Admin re-open.** No Admin re-open of a dismissed flag yet.
- [ ] **Flagged media: nav badge.** No nav badge for unreviewed counts yet.
- [ ] **Flagged media: mobile layout.** No mobile layout hand-off for the review pages yet.
- [ ] **Flagged media: Jamming flag decision.** Not recorded yet.
- [ ] **Combine and remove shared controls.** Review the reusable controls in `.claude/rules/ui-shared.md` (`HistogramTimeline`, `BitrateSeriesChart`, `RssiSeriesChart`, `MediaPlayer`, `TimelineStrip`, `IntervalSecondsInput`, `LocalTimeText`), merge controls that overlap, and delete the ones pages no longer use.

## Current plan (vast-gliding-coral.md)
Full plan: `docs/plans/vast-gliding-coral.md` (31 issues). Status: planned, not started unless marked.
- [ ] **1. Manual sync account selector.** Share provider and account with ScopeState, save to localStorage.
- [ ] **2. Jamming, codec and flag backfill intervals.** Settings in minutes, one per service.
- [ ] **3. Import / Export.** Move menu group, fix empty backup counts and path.
- [ ] **4. Security events unknown-login filter.** Server-side, right panel, saved to localStorage.
- [ ] **5. Loopback IP shows as LocalHost.** On security events and security log.
- [ ] **6. Remove Event Type column** from security events.
- [ ] **7. Merge /admin/pending-credentials into /settings/operators.**
- [ ] **8. Push notifications fixes.** Missing preferences endpoint, subscribe parse, load saved values.
- [ ] **9. Move Notifications and Reports** to System & Network.
- [ ] **10. My Notifications page** under Admin > General (email and push per user).
- [ ] **11. Alert lists capped at 5** with show more (Signal and Bit rate).
- [ ] **12. Week by hour grid.** All-time values, green/yellow/red by std dev.
- [ ] **13. Hover tooltip** on grid boxes (value, week and hour dev).
- [ ] **14. Reading / Deviation radio** on the grid.
- [ ] **15. Signal deviation flags heading** shows raw key. Fix resource.
- [ ] **16. Flag table Type column** shows event type.
- [ ] **17. Dev column** on flag table.
- [ ] **18. Bit rate flags.** Rename heading to "Bit rate alerts", event type and dev.
- [ ] **19. Flag navigator.** Readout on top, buttons side by side, no "flag" in labels.
- [ ] **20. Event type next to playhead time** in histogram player.
- [ ] **21. Auto-advance** to next unreviewed flag after Keep or Dismiss.
- [ ] **22. Previous and Next buttons do nothing.** Missing OnNavigate wiring.
- [ ] **23. Histogram hover** shows value and std dev.
- [ ] **24. Notes panel.** Remove empty message, one-line note box that expands.
- [ ] **25. Remove Recipient description and Passphrase** from /review/export.
- [x] **26. Archive Cloudflare doc.** Done (5f7c5d79).
- [ ] **27. Finish NuGet tasks 3.2, 3.3, 4.1, 4.2.**
- [x] **28. Archive fully done plans.** Done.
- [x] **29. Move open and partly done plans to plans/.** Done.
- [ ] **30. Review and fix 5 GitHub dependency alerts** (2 critical, 3 moderate).
- [ ] **31. test-support location.** Decision pending: keep or move into Utils.Testing.

## Incomplete plans
One line per plan that is not finished. Status is from each plan header; the plan files hold the detail.
- [ ] **media-page-rework.md** (partly done). Batches B1–B8 built; no browser pass yet; follow-ups listed in the plan.
- [ ] **platform-split-and-viewer.md** (partly done). Parts 1–3 merged; WebView2 range playback on MAUI Windows not verified.
- [ ] **flagged-media-review.md** (plan, nothing built). Batch 0 decisions first.
- [ ] **live-view-open-items.md** (partly done). Telemetry sampler, page test and full gate remain.
- [ ] **live-view-telemetry-signalr.md** (open). No remote live-view service on origin/dev.
- [ ] **evidence-store-backup-dr.md** (open). Scheduled backups, media backup, retention, off-box copy, restore verification.
- [ ] **m7-offline-sync-engine.md** (open). No sync engine, cache or conflict policy.
- [ ] **m8-multi-server-sync-secure-storage.md** (open). Depends on M7 for the replica half.
- [ ] **mcp-per-user-and-per-case-scoping.md** (open, conditional on a product decision).
- [ ] **self-service-password-reset-email.md** (open). Reset-by-email not built.
- [ ] **web-push-over-https.md** (open). Deployment decision: HTTPS or no push on LAN.
- [ ] **e2e-web-push-and-toast-delivery.md** (open). Send path has no tests; not verified on a live server.
- [ ] **simple-mode-browser-check.md** (open). Manual browser pass not done.
- [ ] **maui-webauthn-native-e2e.md** (open). Native code has no callers or tests; delete or wire in.
- [ ] **maui-local-app-lock-verification.md** (manual check). Needs a real device.
- [ ] **maui-local-process-log-source.md** (open). No local-process log source.
- [ ] **create-network-shares-todo.md** (open). TODO still in the installer script.
- [ ] **operator-details-admin-alert.md** (pending). Fix is on a local branch, not pushed.
- [ ] **snapshot-timeline.md** (planned). No code written.
- [ ] **SCHEDULED_SYNC_REFACTORED_PLAN.md** (partly complete). Background sync services not built.
- [ ] **scheduled-background-sync-tasks-linux.md** (partly complete). Scheduled loops not built.
- [ ] **chain-reset.md** (draft). Not started except where marked.
- [ ] **device-pair-auth-operators.md** (paused). Nothing implemented yet.
- [ ] **db-field-coverage.md** (in progress). Battery temperature item is still open.
- [ ] **in-place-service-update.md** (status not checked). Some items marked done.
- [ ] **bug-batch-2026-10.md** (status not checked).
- [ ] **IMPLEMENTATION_PLAN.md** (approved). Tasks 1.1–4.2 are mostly open; see Issue 27 in the current plan.
