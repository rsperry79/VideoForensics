# Jamming Analysis Guide

## Overview
VideoForensics MCP server includes RF jamming detection tools for identifying signal interference in Ring device recordings—a common DV (domestic violence) security camera tampering tactic.

## What is Jamming?

**Jamming** = intentional or unintentional RF interference reducing device signal strength (RSSI), causing:
- Video recording gaps
- Missed motion events  
- Device disconnection
- Silent camera failure (victim unaware)

## Jamming Signature

### Normal Conditions
- RSSI fluctuates ±3-5 dB (weather, distance, WiFi)
- Isolated dips routine

### Jamming Indicators
1. **Sustained RSSI drops** — many dB below baseline for multiple events
2. **Abrupt onset/recovery** — not gradual drifting
3. **Temporal clustering** — events in tight window
4. **Recurring pattern** — same time each week (custody handoff, visitor arrival)
5. **Single vs multi-device** — localized jammer vs WiFi issue

## Confidence Levels

| Level | Criteria | Action |
|-------|----------|--------|
| **Low** | Plausible, thin data | Lead to investigate |
| **Medium** | Clearer pattern, environmental factors possible | Surface with hedging |
| **High** | Strong, sustained, unlikely noise | Probable interference |
| **Definite** | Strongest possible pattern | Still statistical inference |

## Database Schema

### JammingIncidentRecords
- Id (GUID)
- DeviceId (GUID)
- StartUtc, EndUtc
- AffectedEventCount (integer)
- AverageDegradationDb (double)
- Confidence (0=Low, 1=Medium, 2=High, 3=Definite)
- DetectedAtUtc
- Notes (max 2000 chars)
- Source (0=AutoDetected, 1=ManuallyRecorded)
- **Index**: DeviceId

### JammingStatsSummaries
- Id (GUID)
- DeviceId (GUID, UNIQUE)
- IncidentCount, TotalJammedDurationMinutes
- AverageDegradationDb, MaxDegradationDb
- Confidence breakdowns (Low/Medium/High/Definite counts)
- FirstIncidentUtc, LastIncidentUtc
- LastUpdatedUtc

## Tool Methods

### RunJammingDetectionAsync(deviceId, fromUtc, toUtc)
- Analyzes device RSSI history
- Persists findings to database
- Returns incident list with confidence

### RecordJammingIncidentAsync(deviceId, startUtc, endUtc, degradationDb, confidence, notes)
- Manually record/correct incident
- Validates time range and degradation
- Marks as ManuallyRecorded for chain of custody
- Recomputes device summary

### GetJammingStatsAsync(deviceId)
- Returns device summary (incident count, duration, severity)
- Empty stats if no incidents

### GetJammingIncidentsAsync(deviceId, fromUtc, toUtc)
- Lists all incidents in time window
- Includes confidence levels and source

## Recommended Workflow

1. **Initial Detection**
   ```
   await jammingTools.RunJammingDetectionAsync(deviceId, startDate, endDate)
   ```

2. **Review Findings** — check confidence, temporal patterns, device health

3. **Manual Corrections (if needed)**
   ```
   await jammingTools.RecordJammingIncidentAsync(...)
   ```

4. **Get Summary**
   ```
   var stats = await jammingTools.GetJammingStatsAsync(deviceId)
   ```

5. **Cross-Reference**
   - Match incident times with known schedules
   - Compare with device connectivity logs
   - Check other devices at location

## Rule Out Alternatives

Before concluding "jamming," check:
- Battery level at incident time
- Firmware updates during incident
- WiFi network changes
- Physical obstruction (foliage, furniture)
- Weather correlation
- Device clock manipulation
- Multi-device WiFi issues

## DV Safety Considerations

**Critical**: Present findings as *evidence consistent with* interference, never confirmed fact.

- Low confidence = lead to investigate, not evidence
- Present each confidence level's count separately
- Recommend corroboration with other evidence
- Avoid false alarm (undermines trust)
- Avoid false reassurance (misses real interference)

### Typical DV Jamming Patterns
- Regular weekly timing (custody handoff, visit window)
- Begins/ends before/after specific person arrives
- Targets specific camera
- Often paired with video/event log gaps

## Example Scenarios

### High-Confidence (DV Case)
```
Front Door Camera - 3 high + 2 medium confidence incidents
Pattern: Every Thursday 6-7 PM (custody handoff)
Total Duration: 312 minutes
Degradation: 12-18 dB below baseline
Verdict: Strong evidence of recurring interference
```

### Low-Confidence (Environmental)
```
Backyard Camera - 1 low confidence incident
Pattern: Random occurrence, scattered across month
Duration: 2 minutes total
Degradation: 3-5 dB (marginal)
Correlation: Matches weather rain events
Verdict: Likely environmental noise
```

## Limitations

1. RSSI-based (inferred, not direct observation)
2. Requires known baseline
3. No jammer device detection
4. Clock-dependent (timestamp accuracy)
5. Environmental sensitivity (weather, appliances, WiFi)

## Status

✅ Ready for Production
- Full MCP integration
- Database schema complete
- Tool methods implemented
- Confidence model with breakdowns
- Chain of custody tracking
- DV safety guidance included

---

**Next**: Deploy MCP server, run detection on case devices, cross-reference with timeline
