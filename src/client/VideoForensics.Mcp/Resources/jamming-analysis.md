# Jamming & Signal Interference Analysis Playbook

This guide is for analyzing Ring device RSSI (signal strength) data for evidence of RF jamming or
interference — a tactic sometimes used to blind or silence security cameras before an in-person
intrusion. It is fetched on demand; it is not injected into every tool call.

## What jamming/interference looks like in RSSI data

- **Normal noise**: RSSI fluctuates within a fairly narrow band tied to weather, walls, other
  household 2.4GHz traffic, and distance from the router. Isolated dips of a few dB are routine.
- **Jamming/interference signature**: a *sustained* drop in RSSI across multiple consecutive events
  for one device, often on the order of many dB below that device's own established baseline, that
  begins abruptly and (usually) recovers abruptly rather than drifting back gradually. A short single
  bad reading is much less meaningful than several consecutive degraded readings clustered in a tight
  window.
- **Recurring temporal pattern** (high confidence for intent): degradation that occurs at the *same
  approximate time each week* — such as during custody handoff times, regular visitor arrivals, or
  scheduled absences — is a strong indicator of intentional, targeted interference rather than
  environmental factors. Environmental interference is random or tied to specific external events
  (weather, appliance startup); deliberate jamming often aligns with planned activities or presence
  windows. Cross-reference incident times against known schedules before concluding environmental
  cause.
- Interference affecting only one device while others at the same location stay normal points more
  toward a localized jammer or physical obstruction near that device; interference affecting every
  device at a location simultaneously more often points to a router/Wi-Fi problem, a household
  appliance, or a broader RF environment change — not necessarily hostile action.

## Recommended tool call sequence

1. **`JammingTools.RunJammingDetection(deviceId, fromUtc, toUtc)`** — pulls the device's raw
   provider events for the window and runs jamming detection against them, persisting any incidents
   found and recomputing that device's summary row. Do this for the specific device and date range
   in question before drawing any conclusion.
2. **Review the returned incidents and their `Confidence` levels** (see below) rather than treating
   every returned incident as equally significant.
3. **`JammingTools.RecordJammingIncident(...)`** — optionally, if you (or a human reviewer) have
   independent reason to add, correct, or annotate an incident that the automatic pass missed or
   mischaracterized, record it manually. Manually recorded incidents are tracked separately
   (`Source = ManuallyRecorded`) from auto-detected ones for chain-of-custody purposes.
4. **`JammingTools.GetJammingStats(deviceId)`** — confirm the device's summary row reflects what you
   expect (incident count, total jammed duration, confidence breakdown) before citing it anywhere.
5. **Include in the signal anomaly report** — `AnalysisTools.BuildSignalAnomalyReport(deviceId,
   fromUtc, toUtc)` reads the persisted jamming stats/incidents for the requested period as part of
   its output, so run the steps above first if the report needs to reflect current findings.

## Interpreting confidence levels

`JammingConfidenceLevel` is `Low`, `Medium`, `High`, or `Definite`:

- **Low** — a plausible pattern, but thin on data (short duration, few affected events, modest
  degradation). Treat as a lead worth investigating further, not a finding.
- **Medium** — a clearer pattern (more affected events, more sustained), but still consistent with
  environmental causes. Worth surfacing but should be phrased with appropriate hedging.
- **High** — a strong, sustained pattern across many events with a degradation magnitude unlikely to
  be routine noise. Reasonable to describe as probable interference, still not proof of intent.
- **Definite** — the strongest signal the detector can produce. Even at this level, this is still
  statistical inference from RSSI, not a direct observation of a jamming device.

**Do not over-claim from sparse data.** A single Low-confidence incident is not evidence of an
attack — it is, at most, something to keep an eye on across future scans. This matters especially in
a DV-safety context: telling a victim or presenting to a court "your camera was jammed" based on thin
evidence risks false alarm (undermining trust in the tool) or false reassurance if a real incident is
later dismissed as noise because an earlier weak one didn't hold up. Always state the confidence
level explicitly when summarizing findings to a human, and avoid stripping the hedge language out of
a summary just to sound more decisive.

## Rule out alternative explanations before concluding "jamming"

Signal degradation can also result from non-hostile causes: a battery running low, firmware updates,
router changes, physical obstruction (furniture, foliage growth), or weather. Before treating a
detected incident as evidence of interference:

- **Check temporal patterns against known schedules.** If degradation occurs at recurring times
  (e.g. every Thursday evening, regular custody handoff times, or predictable visiting hours),
  cross-reference against the victim's timeline of when specific people visit, depart, or have
  access. Regular, predictable timing strongly suggests deliberate interference; random or
  weather-correlated timing suggests environmental causes.
- Check whether the device's system clock may have been tampered with, which can also produce
  timestamp irregularities that masquerade as, or mask, other tampering —
  `ISignalAnomalyDetector.ValidateDeviceTimeAsync` (surfaced via the forensic analysis / chain of
  custody reports) — cross-reference before concluding jamming rather than clock manipulation
  explains an anomaly.
- Check whether the incident correlates with a known event (e.g. a firmware update, a Wi-Fi router
  reboot) rather than an isolated, unexplained drop specific to one device.
- Check whether other devices at the same location were affected at the same time (see above).

## Caveat

Everything above is inference from signal-strength telemetry, not a direct observation of a jamming
device or a confession. Present findings to the end user as *evidence consistent with* interference
at a stated confidence level — never as confirmed fact — and encourage corroborating it with other
evidence (video gaps, event log gaps, physical inspection) before relying on it for a safety decision
or legal proceeding.
