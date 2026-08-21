# Signal Integrity Monitoring for DV Evidence

Automatic detection of signal strength and network issues that may indicate device tampering, jamming, or interference.

## Overview

The metadata extraction system automatically flags evidence for review when signal strength is weak or packet loss is detected. This helps identify potential tampering attempts or environmental interference that could affect evidence reliability.

## Signal Strength Thresholds

### RSSI (Received Signal Strength Indicator)

RSSI measures WiFi signal strength in dBm (decibels relative to one milliwatt):

| RSSI Range | Quality | Reliability | DV Concern |
|------------|---------|-------------|-----------|
| -30 to -40 dBm | Excellent | ✅ Perfect | None |
| -40 to -60 dBm | Good | ✅ Reliable | None |
| -60 to -70 dBm | Fair | ⚠️ May have issues | Possible interference |
| -70 dBm or lower | Poor | ❌ Unreliable | **NEEDS REVIEW** - Likely tampering |

### Thresholds

**Automatic Review Flag Triggers:**

1. **Low Signal**: RSSI ≤ -70 dBm
   - Indicates weak WiFi signal
   - May indicate intentional jamming
   - Could cause recording gaps or quality issues

2. **High Packet Loss**: > 5.0%
   - Indicates network instability
   - May indicate interference or intentional network disruption
   - Could cause video/audio dropouts

## Field Names

### VideoMetadata & SnapshotMetadata

```csharp
// Review Status Fields
public bool NeedsReview { get; set; }              // True if review needed
public string? NeedsReviewReason { get; set; }     // Explanation of why

// Signal Measurements
public int? RssiDbm { get; set; }                  // RSSI in dBm
public double? PacketLossPercent { get; set; }     // Packet loss %
```

## Usage Example

```csharp
var extractor = new MetadataExtractor();
var metadata = extractor.ExtractMetadata(ringEvent);

// Check if review is needed
if (metadata.NeedsReview)
{
    Console.WriteLine($"⚠️ NEEDS REVIEW: {metadata.NeedsReviewReason}");
    Console.WriteLine($"   Signal: {metadata.RssiDbm} dBm");
    Console.WriteLine($"   Packet Loss: {metadata.PacketLossPercent}%");
    
    // Flag for manual investigation
    LogForReview(metadata);
}
else
{
    Console.WriteLine("✅ Signal integrity OK");
    Console.WriteLine($"   Signal: {metadata.RssiDbm} dBm");
    Console.WriteLine($"   Packet Loss: {metadata.PacketLossPercent}%");
}
```

## DV Evidence Implications

### Why This Matters for DV Cases

1. **Perpetrator Interference**: Abusers may attempt to disable or jam Ring devices to prevent evidence recording
2. **Tampering Detection**: Sudden signal loss at critical moments may indicate intentional interference
3. **Evidence Reliability**: Low signal could explain missing footage or audio quality issues
4. **Chain of Custody**: Documents whether device was operating normally

### Investigation Triggers

Flag for law enforcement review if:

```
IF (NeedsReview == true) AND (StreamBroken == true)
    → Likely intentional jamming/tampering

IF (NeedsReview == true) AND (AnomalyScore > 0.7)
    → Possible device interference during suspicious activity

IF (NeedsReview == true) AND (VerifiedDetectionTimestamps.Count == 0)
    → Signal issues may have prevented detection recording
```

## Review Process

### For Law Enforcement

When `NeedsReview` is True:

1. **Examine Signal History**
   - Check if signal was consistently low
   - Look for sudden drops at critical times
   - Compare with other devices in area

2. **Investigate Timing**
   - Does signal loss correlate with incident?
   - Were dropouts brief or prolonged?
   - Did victim report interference?

3. **Environmental Factors**
   - Construction/RF interference nearby?
   - WiFi congestion in area?
   - Distance from router?

4. **Perpetrator Capability**
   - Did perpetrator have access to WiFi?
   - Threat history of technology abuse?
   - Evidence of signal jamming equipment?

## Data Integration

### JSON Export Example

```json
{
  "event_id": "abc123",
  "timestamp": "2026-08-21T14:32:45Z",
  "device_name": "Front Door",
  "needs_review": true,
  "needs_review_reason": "Low signal strength (-78 dBm - may indicate jamming or interference); High packet loss (7.2% - indicates network instability)",
  "rssi_dbm": -78,
  "packet_loss_percent": 7.2,
  "stream_broken": false,
  "anomaly_score": 0.65,
  "person_detected": true,
  "recognized_profiles": [
    {
      "name": "John Doe",
      "confidence": 0.95
    }
  ]
}
```

## Configuration

### Review Thresholds

Current hardcoded thresholds (can be made configurable):

```csharp
// RSSI threshold: -70 dBm or lower
private const int RSSI_THRESHOLD_DBM = -70;

// Packet loss threshold: > 5.0%
private const double PACKET_LOSS_THRESHOLD_PERCENT = 5.0;
```

### To Customize

Modify in `MetadataExtractor.CheckAndFlagForReview()`:

```csharp
private void CheckAndFlagForReview(VideoMetadata metadata)
{
    var reasons = new List<string>();

    // Customize thresholds here
    if (metadata.RssiDbm.HasValue && metadata.RssiDbm.Value <= -65)  // Change -70 to -65
    {
        reasons.Add($"Low signal strength ({metadata.RssiDbm} dBm)");
    }

    if (metadata.PacketLossPercent.HasValue && metadata.PacketLossPercent.Value > 3.0)  // Change 5.0 to 3.0
    {
        reasons.Add($"High packet loss ({metadata.PacketLossPercent}%)");
    }

    if (reasons.Any())
    {
        metadata.NeedsReview = true;
        metadata.NeedsReviewReason = string.Join("; ", reasons);
    }
}
```

## Signal Quality Indicators

### Good Signal (No Review Needed)
- RSSI: -40 to -60 dBm
- Packet Loss: < 1%
- Consistent stable connection
- Reliable video/audio recording

### Acceptable Signal (Monitor)
- RSSI: -60 to -70 dBm
- Packet Loss: 1-5%
- Generally reliable but occasional issues
- May have minor audio/video glitches

### Poor Signal (NEEDS REVIEW) ⚠️
- RSSI: < -70 dBm
- Packet Loss: > 5%
- Frequent connection issues
- Possible recording gaps or quality loss
- **Indicates possible tampering**

## Common Scenarios

### Scenario 1: Legitimate Poor Signal
```
Situation: Ring camera in garage, far from WiFi router
RSSI: -78 dBm
Packet Loss: 6.5%
NeedsReview: true
Reason: "Low signal strength (-78 dBm); High packet loss (6.5%)"

Recommendation: Investigate environmental cause, but event can be used 
if other quality indicators are good (not StreamBroken, high confidence).
```

### Scenario 2: Possible Jamming
```
Situation: Signal was -50 dBm, then suddenly dropped to -85 dBm
Previous Events: Video recorded normally with high confidence
This Event: 
  - RSSI: -85 dBm
  - Packet Loss: 12%
  - StreamBroken: true
  - AnomalyScore: 0.88 (high suspicious activity)
  - PersonDetected: true
  - NeedsReview: true

Red Flags: 
- Sudden signal degradation
- Stream broken flag
- High anomaly during weak signal
- Incident reported by victim at this time

Recommendation: FLAGGED for law enforcement investigation as potential 
deliberate interference during incident.
```

### Scenario 3: Device Offline
```
Situation: Device lost internet connection completely
Previous Events: Recording normally
This Event:
  - RSSI: null (device offline)
  - Packet Loss: null (no network)
  - StreamBroken: true
  - PersonDetected: false (device not recording)
  - NeedsReview: false (no signal data available)

Note: Device offline for extended period during critical time.
```

## Integration with Other Fields

The `NeedsReview` flag works with other evidence fields:

```csharp
// Concerning Combination 1: Interference + Stream Broken
if (metadata.NeedsReview && metadata.StreamBroken)
{
    severity = "CRITICAL - Possible intentional interference";
}

// Concerning Combination 2: Interference + High Anomaly
if (metadata.NeedsReview && metadata.AnomalyScore > 0.7)
{
    severity = "HIGH - Possible tampering during suspicious activity";
}

// Concerning Combination 3: Interference + Sudden Loss
if (metadata.NeedsReview && metadata.RecognizedProfiles?.Count == 0 && 
    previousEvent?.PersonDetected == true)
{
    severity = "HIGH - Lost tracking during interference";
}

// Acceptable: Interference + Normal Operations
if (metadata.NeedsReview && !metadata.StreamBroken && 
    metadata.DetectionConfidence > 0.85)
{
    severity = "LOW - Environmental interference, but recording intact";
}
```

## Forensic Analysis

### For Digital Forensics Experts

When analyzing Ring device evidence with `NeedsReview = true`:

1. **Signal Logs**: Request full RSSI/packet loss history from Ring
2. **Device Logs**: Check device logs for connection drops/reconnects
3. **Network Logs**: Examine WiFi network for interference events
4. **Perpetrator Device**: Check if perpetrator had WiFi jammer or access to network
5. **Timeline Correlation**: Does signal drop align with incident timing?
6. **Alternative Causes**: Rule out environmental/infrastructure explanations

## Testing

The signal monitoring is tested automatically:

```csharp
// Test: Low signal triggers review flag
var metadata = new VideoMetadata
{
    RssiDbm = -75,  // Below -70 threshold
    PacketLossPercent = 3.0  // Below 5% threshold
};
CheckAndFlagForReview(metadata);
Assert.IsTrue(metadata.NeedsReview);
Assert.Contains("Low signal", metadata.NeedsReviewReason);

// Test: High packet loss triggers review flag
metadata = new VideoMetadata
{
    RssiDbm = -50,  // Good signal
    PacketLossPercent = 8.5  // Above 5% threshold
};
CheckAndFlagForReview(metadata);
Assert.IsTrue(metadata.NeedsReview);
Assert.Contains("High packet loss", metadata.NeedsReviewReason);

// Test: Both trigger combined reason
metadata = new VideoMetadata
{
    RssiDbm = -72,
    PacketLossPercent = 6.2
};
CheckAndFlagForReview(metadata);
Assert.IsTrue(metadata.NeedsReview);
Assert.Contains("Low signal", metadata.NeedsReviewReason);
Assert.Contains("High packet loss", metadata.NeedsReviewReason);
```

## Future Enhancements

1. **Configurable Thresholds**: Make RSSI/packet loss thresholds configurable per organization
2. **Historical Trend Analysis**: Track signal quality over time for correlation
3. **Multi-Device Analysis**: Compare signal across multiple Ring devices
4. **Automated Alerts**: Real-time notifications when signal integrity issues detected
5. **Severity Scoring**: Calculate overall evidence reliability score
6. **Network Analysis**: Integration with network forensics for interference detection

## References

- **RSSI Reference**: https://en.wikipedia.org/wiki/Received_signal_strength_indication
- **WiFi Signal Standards**: IEEE 802.11 specification
- **Packet Loss Analysis**: Network performance diagnostics
- **Ring Device Documentation**: Device health monitoring
- **DV Evidence Standards**: Chain of custody for digital evidence
