# Metadata Capture Implementation Roadmap

## Status: ALL PHASES COMPLETE ✅

Complete metadata capture implementation for forensics and audit purposes.

## Phase 1: Infrastructure (COMPLETED) ✅

### Changes Made
- Updated `IVideoForensicsDataClient.EnsureLocationAsync()` to accept optional `metadataJson` and `apiResponseHash` parameters
- Updated `IVideoForensicsDataClient.EnsureDeviceAsync()` to accept optional `metadataJson` and `apiResponseHash` parameters
- Both methods now store metadata in database entities when created
- Added `LastSyncedUtc` and `SyncStatus = Synced` tracking

### Commits
- `4d82682` - Integrate self-tester with RingAuthService and update database path
- `0d30689` - Add metadata capture infrastructure to data layer

## Phase 2: Service Layer Integration (COMPLETED) ✅

### Overview
The service layer needs to serialize API responses and pass them to the data layer.

### Implementation Pattern

```csharp
// Helper method to serialize and hash API responses
private (string Json, string Hash) SerializeMetadata(object apiResponse)
{
    using var hash = System.Security.Cryptography.SHA256.Create();
    var json = System.Text.Json.JsonSerializer.Serialize(apiResponse);
    var hashValue = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
    var hashHex = System.Convert.ToHexString(hashValue);
    return (json, hashHex);
}

// When calling EnsureLocationAsync
var metadata = SerializeMetadata(ringApiLocationObject);
var location = await _dataClient.EnsureLocationAsync(
    providerAccountId,
    locationId,
    name,
    address,
    metadata.Json,      // NEW: Full API response as JSON
    metadata.Hash       // NEW: SHA256 hash for change detection
);

// When calling EnsureDeviceAsync  
var metadata = SerializeMetadata(ringApiDeviceObject);
var device = await _dataClient.EnsureDeviceAsync(
    locationId,
    providerDeviceId,
    name,
    type,
    isOnline,
    metadata.Json,      // NEW: Full API response as JSON
    metadata.Hash       // NEW: SHA256 hash for change detection
);
```

### Files to Update

1. **`src/client/core/VideoForensics.Client.Core/Services/VideoDownloadServiceAdapter.cs`**
   - Add using directives: `System.Text.Json`, `System.Security.Cryptography`
   - Add `SerializeMetadata()` helper method
   - Update all `EnsureLocationAsync()` calls (~line 65) to pass metadata
   - Update all `EnsureDeviceAsync()` calls (~line 70) to pass metadata

2. **Any other service layer that calls these methods** (if found)

### Testing
- Self-tester with `--verify-db` will confirm metadata is being captured
- Check database: `SELECT MetadataJson, ApiResponseHash FROM Locations LIMIT 1;`
- Verify metadata is no longer null

## Benefits

Once Phase 2 is complete:
- ✅ Full API response history in database for audit trail
- ✅ Hash-based change detection for reconciliation
- ✅ Complete forensics data for investigations
- ✅ Self-tester will confirm all API data points are recorded

## Phase 3: Event & MediaItem Metadata (COMPLETED) ✅

### Completed Changes
- Added SerializeMetadata helper to RingMediaDownloadService
- All UpsertEventRecordAsync calls pass API response for serialization
- MediaItem entity extended with MetadataJson and ApiSourceHash
- Video downloads and snapshot downloads store metadata
- Events table captures full DoorbotHistoryEvent metadata

## Phase 4: Database Verification (COMPLETED) ✅

### Completed Changes
- Self-tester `--verify-db` now displays metadata capture statistics
- Event and MediaItem metadata completeness percentage
- Device and location metadata counts
- Graceful handling for schemas without metadata columns

## Documentation (COMPLETED) ✅

- FORENSICS_AUDIT_GUIDE.md - Comprehensive audit trail analysis guide
- METADATA_MIGRATION_GUIDE.md - Migration and schema update guide
- API documentation updated to reflect metadata tracking

## Implementation Complete

All phases are now complete. Metadata capture is fully integrated throughout VideoForensics:

✅ Data layer infrastructure  
✅ Service layer integration  
✅ Event and MediaItem metadata  
✅ Database verification  
✅ Forensics audit documentation  
✅ Migration guide for existing databases

To use metadata:
1. Run normal collections - metadata is captured automatically
2. Run self-tester `--verify-db` to verify metadata completeness
3. See FORENSICS_AUDIT_GUIDE.md for forensics query examples
4. See METADATA_MIGRATION_GUIDE.md for migration and storage considerations
