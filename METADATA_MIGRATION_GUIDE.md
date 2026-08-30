# Metadata Migration Guide

## Overview

VideoForensics v2 introduces comprehensive metadata capture for forensic auditing. This guide helps you migrate existing databases and understand the metadata schema changes.

## What's New

### New Metadata Fields

#### Device Table
- `MetadataJson` (TEXT): Full Ring API device response
- `ApiSourceHash` (TEXT): SHA256 hash of MetadataJson

#### Location Table  
- `MetadataJson` (TEXT): Full Ring API location response
- `ApiSourceHash` (TEXT): SHA256 hash of MetadataJson

#### Event Table
- `MetadataJson` (TEXT): Serialized DoorbotHistoryEvent
- `ApiSourceHash` (TEXT): SHA256 hash for change detection

#### MediaItem Table (New)
- `MetadataJson` (TEXT): Download/snapshot metadata
- `ApiSourceHash` (TEXT): Hash for integrity verification

## Migration Path

### Automatic (No Action Required)

The database schema includes metadata fields, but they are **optional**:

1. **Existing databases continue to work** - Old records have NULL metadata
2. **New records capture metadata** - Next download/collection run populates new fields
3. **No schema migration needed** - Fields exist but aren't required

### Verifying Migration Status

Check metadata completeness:

```bash
# Run self-tester to see metadata capture %
dotnet run src/selftest -- --verify-db
```

Example output:
```
Metadata capture: Events: 45% (90/200), MediaItems: 50% (50/100)
  Devices with metadata: 3, Locations: 1
```

This indicates:
- 45% of events have metadata (55% are older, pre-metadata records)
- 50% of media items have metadata
- Only 3 devices and 1 location have been re-scanned since update

### Progressive Population Strategy

Metadata is populated automatically as you use VideoForensics:

1. **Normal collection** - Next download run captures metadata for new events
2. **Gap refills** - Re-running `--verify-db` or collection for older dates populates metadata
3. **Full coverage** - After 1-2 months of normal use, metadata should be >90% populated

### Accelerated Migration (Optional)

To populate metadata faster:

```bash
# Re-run device discovery and download from last 30 days
dotnet run src/clients/VideoForensics -- \
  --start-date 2024-12-01 \
  --force

# Then verify
dotnet run src/selftest -- --verify-db
```

The `--force` flag re-downloads events and captures new metadata even if files exist locally.

## Database Queries for Migration

### Count Records by Metadata Status

```sql
-- Events
SELECT 
  'Events' as TableName,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata,
  COUNT(CASE WHEN MetadataJson IS NULL THEN 1 END) as WithoutMetadata,
  ROUND(100.0 * COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) / COUNT(*), 1) as PercentWithMetadata
FROM Events

UNION ALL

-- MediaItems
SELECT 
  'MediaItems' as TableName,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata,
  COUNT(CASE WHEN MetadataJson IS NULL THEN 1 END) as WithoutMetadata,
  ROUND(100.0 * COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) / COUNT(*), 1) as PercentWithMetadata
FROM MediaItems

UNION ALL

-- Devices
SELECT 
  'Devices' as TableName,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata,
  COUNT(CASE WHEN MetadataJson IS NULL THEN 1 END) as WithoutMetadata,
  ROUND(100.0 * COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) / COUNT(*), 1) as PercentWithMetadata
FROM Devices

UNION ALL

-- Locations
SELECT 
  'Locations' as TableName,
  COUNT(*) as Total,
  COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) as WithMetadata,
  COUNT(CASE WHEN MetadataJson IS NULL THEN 1 END) as WithoutMetadata,
  ROUND(100.0 * COUNT(CASE WHEN MetadataJson IS NOT NULL THEN 1 END) / COUNT(*), 1) as PercentWithMetadata
FROM Locations;
```

### Find Oldest Records Without Metadata

```sql
SELECT 
  'Events' as Source,
  OccurredAtUtc as Timestamp,
  COUNT(*) as RecordCount
FROM Events
WHERE MetadataJson IS NULL
GROUP BY DATE(OccurredAtUtc)
ORDER BY OccurredAtUtc ASC
LIMIT 10;
```

This shows which date ranges need re-collection for metadata population.

### Estimate Storage Impact

```sql
SELECT 
  'Events' as TableName,
  COUNT(*) as RecordsWithMetadata,
  ROUND(SUM(LENGTH(MetadataJson)) / 1024.0 / 1024.0, 2) as MetadataSize_MB,
  ROUND(SUM(LENGTH(MetadataJson)) / CAST(COUNT(*) AS FLOAT) / 1024.0, 2) as AvgSize_KB
FROM Events
WHERE MetadataJson IS NOT NULL

UNION ALL

SELECT 
  'MediaItems' as TableName,
  COUNT(*) as RecordsWithMetadata,
  ROUND(SUM(LENGTH(MetadataJson)) / 1024.0 / 1024.0, 2) as MetadataSize_MB,
  ROUND(SUM(LENGTH(MetadataJson)) / CAST(COUNT(*) AS FLOAT) / 1024.0, 2) as AvgSize_KB
FROM MediaItems
WHERE MetadataJson IS NOT NULL;
```

Example output:
```
Events: 1500 records, 15.3 MB total, ~10.2 KB each
MediaItems: 800 records, 2.1 MB total, ~2.6 KB each
```

## Backup Recommendations

Before running collection with metadata capture:

```bash
# SQLite database backup
cp ProgramData/VideoForensics/videoforensics.db \
   ProgramData/VideoForensics/videoforensics.db.backup

# Then run collection
dotnet run src/clients/VideoForensics -- --start-date 2024-01-01
```

## Troubleshooting

### Metadata columns don't exist

If you see "no such column" errors:

1. Ensure you're running VideoForensics v2 or later
2. Database migration runs automatically on first app launch
3. Restart the application if schema update fails

### Metadata queries return NULL

This is normal for older records. To populate:

```bash
# Re-run collection for specific date range
dotnet run src/clients/VideoForensics -- \
  --start-date 2024-11-01 \
  --end-date 2024-12-31 \
  --force
```

### Storage concerns

If database grows too large:

1. Archive old metadata: `DELETE FROM Events WHERE OccurredAtUtc < '2023-01-01' AND MetadataJson IS NOT NULL;`
2. Export metadata before deletion: See FORENSICS_AUDIT_GUIDE.md
3. Compression: SQLite can compress JSON fields via `VACUUM` after deletion

## FAQ

**Q: Do I need to do anything to use metadata?**
A: No. Metadata is automatically captured on next collection run.

**Q: Will old records get metadata?**
A: Only if you re-download them or re-run collection with `--force`.

**Q: Can I disable metadata capture?**
A: Metadata capture is automatic but optional (NULL is valid). Remove data with SQL if not needed.

**Q: How much space does metadata use?**
A: ~10KB per event, ~2-3KB per media item. Total impact is typically 5-15MB for a year of data.

**Q: Is metadata required for forensics features?**
A: No, but it greatly improves audit trail quality. Run collection with `--force` to populate it.
