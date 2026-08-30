# VideoForensics Audit & Metadata Guide

## Overview

VideoForensics now captures complete API response metadata for every device, location, event, and media item. This enables forensic analysis, compliance auditing, and change detection across your Ring account history.

## Metadata Captured

### Locations & Devices
- **MetadataJson**: Full Ring API response for location/device
- **ApiSourceHash**: SHA256 hash of metadata for change detection

### Events
- **MetadataJson**: Serialized DoorbotHistoryEvent from Ring API
- **ApiSourceHash**: SHA256 hash for integrity verification
- **EventIntegrityHash**: Hash of the downloaded video file (if downloaded)

### MediaItems  
- **MetadataJson**: Snapshot of download metadata (timestamp, event ID, type)
- **ApiSourceHash**: Hash for change tracking
- **Sha256Hash**: Hash of the downloaded file

## Forensic Queries

### Find All Events with Metadata
```sql
SELECT 
  DeviceId,
  ProviderEventId,
  EventType,
  OccurredAtUtc,
  length(MetadataJson) as MetadataBytes,
  ApiSourceHash
FROM Events
WHERE MetadataJson IS NOT NULL
ORDER BY OccurredAtUtc DESC;
```

### Detect Device Changes
```sql
SELECT 
  ProviderDeviceId,
  Name,
  COUNT(*) as VersionCount,
  COUNT(DISTINCT ApiSourceHash) as UniqueHashes
FROM Devices
WHERE MetadataJson IS NOT NULL
GROUP BY ProviderDeviceId
HAVING COUNT(DISTINCT ApiSourceHash) > 1
ORDER BY VersionCount DESC;
```

### Timeline of Location Activity
```sql
SELECT 
  l.ProviderLocationId,
  l.Name,
  COUNT(e.id) as EventCount,
  MIN(e.OccurredAtUtc) as FirstEvent,
  MAX(e.OccurredAtUtc) as LastEvent
FROM Locations l
LEFT JOIN Devices d ON l.Id = d.LocationId
LEFT JOIN Events e ON d.Id = e.DeviceId
WHERE l.MetadataJson IS NOT NULL
GROUP BY l.Id, l.ProviderLocationId, l.Name
ORDER BY LastEvent DESC;
```

### Verify Download Integrity
```sql
SELECT 
  m.FileName,
  m.Sha256Hash as FileHash,
  m.ApiSourceHash as MetadataHash,
  m.IntegrityVerified,
  CASE 
    WHEN m.Sha256Hash = m.ApiSourceHash THEN 'Match'
    ELSE 'Mismatch'
  END as IntegrityStatus
FROM MediaItems m
WHERE m.Sha256Hash IS NOT NULL
  AND m.ApiSourceHash IS NOT NULL
ORDER BY m.DownloadedAtUtc DESC;
```

### Find Events Without Downloaded Media
```sql
SELECT 
  e.ProviderEventId,
  e.EventType,
  e.OccurredAtUtc,
  e.SnapshotUrl,
  CASE WHEN m.Id IS NULL THEN 'Not Downloaded' ELSE 'Downloaded' END as Status
FROM Events e
LEFT JOIN MediaItems m ON e.Id = m.DownloadEventId
WHERE e.MetadataJson IS NOT NULL
ORDER BY e.OccurredAtUtc DESC;
```

## Audit Trail Analysis

### Account Activity Summary
```sql
SELECT 
  'Locations' as Category,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata
FROM Locations

UNION ALL

SELECT 
  'Devices' as Category,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata
FROM Devices

UNION ALL

SELECT 
  'Events' as Category,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata
FROM Events

UNION ALL

SELECT 
  'MediaItems' as Category,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata
FROM MediaItems;
```

### Self-Tester Verification

Run the self-tester with metadata verification:

```bash
dotnet run -- --verify-db
```

The report will show:
- Device/location database completeness
- Metadata capture percentage for Events and MediaItems
- Devices and locations with metadata records

Example output:
```
Metadata capture: Events: 95% (190/200), MediaItems: 98% (98/100)
  Devices with metadata: 5, Locations: 2
```

## Compliance & Legal Holds

### Preserving Evidence
1. Metadata captures the exact state of your Ring account at time of forensic run
2. ApiSourceHash enables detection of tampering or modification
3. Full API responses provide complete audit trail for legal proceedings

### Export for Legal Review
```sql
-- Export events with metadata for legal hold
SELECT 
  e.ProviderEventId,
  e.EventType,
  e.OccurredAtUtc,
  e.DiscoveredAtUtc,
  e.MetadataJson,
  e.ApiSourceHash,
  m.FilePath,
  m.Sha256Hash
FROM Events e
LEFT JOIN MediaItems m ON e.Id = m.DownloadEventId
WHERE e.OccurredAtUtc >= datetime('2024-01-01')
ORDER BY e.OccurredAtUtc;
```

## Migration Guide for Existing Databases

### Adding Metadata to Existing Records

If you have an existing database without metadata, you can re-run collections to populate metadata:

1. **Run the main app normally** - New downloads will capture metadata automatically
2. **Use self-tester --verify-db** - Check metadata completeness:
   ```bash
   dotnet run src/selftest -- --verify-db
   ```
3. **Missing metadata indicates** - Events/devices that haven't been re-downloaded since the metadata feature was added

### Progressive Migration Strategy

- New downloads automatically capture metadata
- Existing media items without metadata indicate older downloads (pre-metadata feature)
- No data migration required - metadata is optional and captured going forward
- Full historical metadata is captured on next collection run

## Performance Notes

- Metadata storage adds ~1-5KB per event (JSON size varies)
- Hashing adds negligible overhead (~1ms per record)
- Queries with metadata are indexed and performant
- Archive old metadata if database size becomes a concern

## Future Enhancements

- [ ] Metadata-based change detection (auto-flag when device config changes)
- [ ] API comparison utilities (diff two metadata captures)
- [ ] Timeline visualization with metadata annotations
- [ ] Export metadata to forensic analysis tools
