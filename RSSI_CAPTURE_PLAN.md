# RSSI Capture Implementation Plan

## Problem
- Ring API **provides** RSSI (signal strength) in each event
- Our database **stores** metadata JSON (which may contain it)
- But we **don't query** it efficiently for jamming detection
- Result: Jamming detection framework is built but has no data to analyze

## What's Available in Ring API

### Per-Event RSSI (in DownloadedEventRecord)
```json
{
  "id": "event-123",
  "device_health": {
    "rssi": -42,                    // Signal strength (dBm)
    "rssi_category": "good",        // good/fair/poor/offline
    "latest_signal_strength": -42,
    "average_signal_strength": -45
  }
}
```

### Device Health Endpoint
```
GET https://api.ring.com/clients_api/doorbots/{id}/health
```
Returns current battery, connectivity, RSSI

## Implementation Plan

### Phase 1: Add RSSI Fields to Event Entity
**File**: `src/data/common/data.common/Entities/Event.cs`

```csharp
public class Event
{
    // ... existing fields ...
    
    // New RSSI fields for jamming detection
    public int? RssiDbm { get; set; }              // -100 to 0 dBm
    public string? RssiCategory { get; set; }      // good/fair/poor/offline
    public double? AverageSignalStrength { get; set; }
    public DateTime? HealthSnapshotUtcTime { get; set; }
}
```

### Phase 2: Create Migration
**Command**: 
```bash
dotnet ef migrations add AddRssiFieldsToEvent -p src/data/database/sqlite/data.database.sqlite -s src/data/database/sqlite/data.database.sqlite
```

**Migration**:
- Add 4 nullable columns to Events table
- Create index on DeviceId + OccurredAtUtc + RssiDbm for jamming queries

### Phase 3: Extract RSSI During Event Capture
**File**: `src/providers/ring/provider/Services/RingMediaDownloadService.cs`

When upserting events:
```csharp
var rssi = @event.DeviceHealth?.Rssi;
var rssiCategory = @event.DeviceHealth?.RssiCategory;

await _dataClient.UpsertEventAsync(
    new Event 
    { 
        // ... existing ...
        RssiDbm = rssi,
        RssiCategory = rssiCategory,
        AverageSignalStrength = @event.DeviceHealth?.AverageSignalStrength,
        HealthSnapshotUtcTime = @event.OccurredAtUtc
    }
);
```

### Phase 4: Enable Jamming Detection
Once RSSI is queryable:
1. **Baseline Calculation** — first N events establish normal RSSI range
2. **Anomaly Detection** — sustained drops below baseline = potential jamming
3. **Pattern Analysis** — recurring times = high confidence
4. **Database Storage** — incidents persisted (already implemented)

## SQL Query Example (Post-Implementation)

```sql
-- Find events with significant RSSI degradation
SELECT 
  e.OccurredAtUtc,
  e.RssiDbm,
  (SELECT AVG(RssiDbm) FROM Events WHERE DeviceId = e.DeviceId LIMIT 10) AS baseline,
  (e.RssiDbm - baseline) AS degradation_db
FROM Events e
WHERE e.DeviceId = ?
  AND e.RssiDbm IS NOT NULL
  AND (e.RssiDbm - baseline) < -8  -- significant drop
ORDER BY e.OccurredAtUtc
```

## Impact

### Before
```
AnalyzeJammingAsync(device) 
→ No RSSI data in DB
→ Can't detect patterns
→ Returns empty results ❌
```

### After
```
AnalyzeJammingAsync(device)
→ Query RSSI history
→ Analyze patterns vs baseline
→ Return jamming incidents ✅
```

## Effort Estimate
- Phase 1: 15 min (add entity fields)
- Phase 2: 10 min (generate migration)
- Phase 3: 30 min (extract RSSI during capture)
- Phase 4: 30 min (update jamming detection logic)
- **Total**: ~1.5 hours

## Dependencies
- Ring API already provides RSSI ✅
- Event entity ready for fields ✅
- Jamming framework ready ✅
- Only missing: RSSI capture + query

## Testing
1. Download events from test Ring account
2. Verify RSSI fields populated
3. Run AnalyzeJammingAsync() on device
4. Should detect baseline + any signal anomalies

## Next Steps
Implement Phases 1-4 to enable complete jamming detection workflow
