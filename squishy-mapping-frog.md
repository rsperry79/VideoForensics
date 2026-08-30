# Remaining Work - VideoForensics

## Active: Metadata Capture Phase 2

### Description
Complete API response metadata serialization in service layer to store full API responses in database for forensics and audit purposes.

### Files to Update
1. **`src/client/core/VideoForensics.Client.Core/Services/VideoDownloadServiceAdapter.cs`**
   - Add `System.Text.Json` and `System.Security.Cryptography` using directives
   - Add `SerializeMetadata(object apiResponse)` helper method to serialize and hash API responses
   - Update all `EnsureLocationAsync()` calls to pass `metadataJson` and `apiResponseHash`
   - Update all `EnsureDeviceAsync()` calls to pass `metadataJson` and `apiResponseHash`

### Implementation Details
See `METADATA_CAPTURE_ROADMAP.md` for:
- Code examples and patterns
- Expected behavior
- Testing approach

### Acceptance Criteria
- [ ] Metadata parameters added to both Ensure methods
- [ ] SerializeMetadata helper implemented
- [ ] All calls to EnsureLocationAsync pass metadata
- [ ] All calls to EnsureDeviceAsync pass metadata
- [ ] Build succeeds with no errors
- [ ] Self-tester `--verify-db` runs successfully
- [ ] Database queries show non-null MetadataJson and ApiResponseHash values

---

## Completed Work

### Self-Tester Integration (✅ Complete)
- Integrated with RingAuthService (same auth as main app)
- Updated database path: `ProgramData/VideoForensics` with `AppData` fallback
- Self-tester now loads credentials from application's auth system
- Commit: `4d82682`

### Metadata Capture Infrastructure (✅ Complete)
- Data layer updated to accept metadata parameters
- `IVideoForensicsDataClient` interface updated
- `EnsureLocationAsync()` and `EnsureDeviceAsync()` ready for metadata
- Commit: `0d30689`

### Documentation (✅ Complete)
- `METADATA_CAPTURE_ROADMAP.md` created with implementation guide
- Clear patterns and examples provided
- Commit: `8d6f2f3`

---

## Future Work (Not Started)

### Event/MediaItem Metadata
- [ ] Update Event creation in `RingEventAndConfigService`
- [ ] Update MediaItem creation in `RingMediaDownloadService`
- [ ] Add metadata parameters to `UpsertEventAsync()`

### Database Verification
- [ ] Add metadata validation to self-tester completeness report
- [ ] Ensure metadata is populated for all API data points
- [ ] Add forensics query utilities for metadata inspection

### Documentation
- [ ] Update API documentation to reflect metadata tracking
- [ ] Add forensics/audit guide for using stored metadata
- [ ] Create migration guide for existing databases

---

## Notes

- All infrastructure is backward compatible (metadata parameters are optional)
- Metadata capture enables complete forensics audit trail
- Self-tester `--verify-db` will confirm all data points are recorded
- Implementation follows existing patterns in codebase
