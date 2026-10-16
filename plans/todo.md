# To-do

## Blocked
- [ ] **Calibrate media-analysis thresholds on real footage.** Blocked: no downloaded clips exist yet, so nothing can be checked against real Ring footage. Needs downloads (provider calls) to be approved first. Covers: brightness (`DarkLumaCeiling`), saturation (`GrayscaleSaturationCeiling`), clipping (`FullScaleToleranceDb`, `ClippedPeakThreshold`), noise rise (`NoiseFloorRiseThresholdDb`), dropouts (`DropoutGapThreshold`, `DropoutSilenceRiseFraction`), bitrate collapse (`SuspectedFraction`), minimum baseline size (`MinimumBucketSamples`), and the history window (`HistoryWindowClips`).

## Open
- [ ] HDR brightness: after the 8-bit conversion, check that true HDR clips are not misclassified as dark or day.
- [ ] Confirm `silencedetect` and `astats` output on a real Ring clip (the Ring audio is AAC in MP4).
- [ ] Step-up checks for account removal and two-factor override: needs an operator with a passkey to test end to end.
- [ ] Wait for analysis before the next provider call across all providers (currently Ring only, via the shared data client).
- [ ] Initial sync on a new account: run a real add-account flow end to end and confirm the first full sync pulls history and devices.
- [ ] Ring event retention: undefined. The integration has no retention setting, so decide what to record.
- [ ] Light and night modes: check whether Ring reports the current day or night state; the integration only exposes night-mode and floodlight controls.
- [ ] Hemisphere is fixed to northern (`MediaAnalysisService.CameraHemisphere`); make it a per-install setting.
- [ ] Drift detector is built (`DriftDetector`) but not wired: store a notice for review when a device drifts.
