# To-do

## Blocked
- [ ] **Calibrate media-analysis thresholds on real footage.** Blocked: no downloaded clips exist yet, so nothing can be checked against real Ring footage. Needs downloads (provider calls) to be approved first. Covers: brightness (`DarkLumaCeiling`), saturation (`GrayscaleSaturationCeiling`), clipping (`FullScaleToleranceDb`, `ClippedPeakThreshold`), noise rise (`NoiseFloorRiseThresholdDb`), dropouts (`DropoutGapThreshold`, `DropoutSilenceRiseFraction`), bitrate collapse (`SuspectedFraction`), minimum baseline size (`MinimumBucketSamples`), and the history window (`HistoryWindowClips`).
- [ ] **HDR brightness.** Blocked: needs real HDR clips. Check that true HDR footage is not misclassified as dark or day after the 8-bit conversion.
- [ ] **Confirm `silencedetect` and `astats` output on a real Ring clip.** Blocked: needs downloaded Ring clips with audio (AAC in MP4).
- [ ] **Initial sync on a new account, end to end.** Blocked: needs a real Ring sign-in through the add-account wizard, which only you can do.

## Open
- [ ] **Nov 1: confirm the `health.battery_temp` unit.** Stored as deci-Kelvin (`2921` = 18.95 °C = 66 °F) because a wired Front Door doorbell reading 24 °F above a 42 °F evening (98223, 2026-10-30 02:56 UTC, ambient 5.6 °C) is plausible; hundredths of °C (29.21 °C) would be 42 °F above ambient. Pull the stored readings for Oct 31 and Nov 1 and compare with the hourly ambient temperature (Open-Meteo, lat 48.20, lon -122.13). Deci-Kelvin moves about 10 raw units per °C, hundredths of °C about 100. If the slope says hundredths of °C, change the conversion. `status_time` is settled: ticks of a 32768 Hz clock since the Unix epoch.
- [ ] Step-up checks for account removal and two-factor override: needs an operator with a passkey to test end to end.
- [ ] Ring event retention: undefined. The integration has no retention setting, so decide what to record.
- [ ] Light and night modes: check whether Ring reports the current day or night state; the integration only exposes night-mode and floodlight controls.
- [x] Hemisphere is a configured setting (`MediaAnalysis:Hemisphere`, default northern).
- [x] Drift is recorded on each device (`BaselineDriftDetected`, `BaselineDriftChangeFraction`) for review.
- [x] Analysis is awaited before the next provider call; the shared data client does this for every provider that records downloads (today: Ring).
