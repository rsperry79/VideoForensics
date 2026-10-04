# Phase 2 Implementation Plan: Automatic Event/Snapshot Pull on Account Connect

## Context
Phase 2 adds automatic data pulling when users connect provider accounts. On successful account authentication, the system should pull events and device configuration from the last known timestamp (or all available data for first-time connects). A manual "Sync Now" button provides on-demand refresh capability.

## Key Findings from Codebase
- **ProviderAccount** already has `LastSuccessfulAuthUtc` and `LastErrorMessage` fields
- **Device** entity has per-device tracking: `LastSuccessfulPullAtUtc`, `LastPullAttemptAtUtc`
- **Event** entity stores forensic audit trail: `OccurredAtUtc`, `DiscoveredAtUtc`, `DownloadedAtUtc`
- **IEventAndConfigService** interface exists with `GetEventsAsync()` and `GetDeviceConfigAsync()` methods
- **SyncSchedule** entity tracks separate event and snapshot pull intervals
- Fire-and-forget pattern established in codebase (DownloadEndpoints uses `Task.Run()` + `Results.Accepted()`)
- UI patterns: SfToast for notifications, SfProgressBar for loading spinners

## Implementation Approach

### 1. Data Model Changes
**ProviderAccount** entity already has required fields:
- `LastSuccessfulAuthUtc` (DateTime?) - will be set to pull completion time
- `LastErrorMessage` (string?) - will be populated on pull failure
- Note: Explore if `LastDownloadTimeUtc` conflicts or should be used instead

### 2. Business Logic: EventPullService
New service: `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs`

Responsibilities:
- `PullAccountEventsAsync(accountId, fromTimestampUtc, cancellationToken)` - pulls events/config
  - If `fromTimestampUtc` is null: pull all available data (first-time connect)
  - If `fromTimestampUtc` is set: pull from that timestamp forward
  - Calls `IEventAndConfigService.GetEventsAsync()` for all devices
  - Also calls `GetDeviceConfigAsync()` for each device
  - Updates `ProviderAccount.LastSuccessfulAuthUtc` on success (stores pull completion time)
  - Sets `ProviderAccount.LastErrorMessage` on failure (surfaces to UI)
  - Per-device tracking via `Device.LastSuccessfulPullAtUtc` and `Device.LastPullAttemptAtUtc`
  - Fires background tasks (non-blocking, fire-and-forget)

### 3. API Integration

**Modify:** `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs`
- After successful POST /api/v1/accounts/add, fire background pull using fire-and-forget pattern:
  ```csharp
  _ = Task.Run(async () => {
      try {
          await eventPullService.PullAccountEventsAsync(accountId, null, ct);
      }
      catch { /* Error stored in ProviderAccount.LastErrorMessage */ }
  }, ct);
  ```
- Return endpoint immediately with normal success response (does not wait for pull)

**New endpoint:** POST /api/v1/accounts/{accountId}/sync-now
- Calls `EventPullService.PullAccountEventsAsync(accountId, fromTimestamp, ct)`
- Fire-and-forget pattern: returns `Results.Accepted()` with HTTP 202
- Response DTO: `SyncNowResponseDto` (Success, Message, SyncStartedAtUtc)
- Client can poll or check `ProviderAccount.LastErrorMessage` for failures

**New DTOs** (add to `src/core/api/VideoForensics.Api.Contracts/AccountDtos.cs`):
- `SyncNowResponseDto` (Success: bool, Message: string, SyncStartedAtUtc: DateTime)

### 4. UI: AccountDetails.razor

Add "Sync Now" button with async handling:
- Pattern from `Evidence.razor`: Click button → fire POST → show spinner → toast on completion
- Spinner: `<SfProgressBar Type="ProgressType.Circular" IsIndeterminate="true" />`
- Toast: `<SfToast @ref="_toastRef" />` → show with icon ("e-success", "e-warning", "e-danger")
- Display last pull timestamp (`ProviderAccount.LastSuccessfulAuthUtc`) in account details section
- Display `ProviderAccount.LastErrorMessage` inline if error occurred
- Button disabled while `_isSyncing` true
- Follow try-catch-finally pattern from `Notifications.razor`

**UI state fields:**
- `_isSyncing` (bool) - tracks pull in progress
- `_toastRef` (SfToast) - for notifications
- Error/success messages from `ProviderAccount.LastErrorMessage` and `ProviderAccount.LastSuccessfulAuthUtc`

### 5. Testing Strategy

**EventPullService unit tests (xUnit + Moq):**
- `PullAccountEventsAsync_FirstConnect_PullsAllData` - fromTimestampUtc=null, pulls all events
- `PullAccountEventsAsync_ExistingAccount_PullsSinceLastAuth` - fromTimestampUtc set, filters by timestamp
- `PullAccountEventsAsync_CallsGetEventsAndConfigForAllDevices` - verifies IEventAndConfigService methods called for each device
- `PullAccountEventsAsync_Success_UpdatesAccountTimestamp` - ProviderAccount.LastSuccessfulAuthUtc updated
- `PullAccountEventsAsync_Failure_SetsErrorMessage` - ProviderAccount.LastErrorMessage populated
- `PullAccountEventsAsync_NoDevices_CompletesSuccessfully` - handles account with no devices

**AccountEndpoints integration tests:**
- POST /api/v1/accounts/add response payload (no blocking wait for pull)
- POST /api/v1/accounts/{id}/sync-now returns 202 Accepted with SyncNowResponseDto
- Verify EventPullService was invoked via dependency injection

**UI verification (manual):**
- Button renders and is clickable in AccountDetails
- Click fires POST to /api/v1/accounts/{id}/sync-now
- Spinner appears during request
- Toast shows on success (check ProviderAccount.LastSuccessfulAuthUtc updated)
- Toast shows on error (check ProviderAccount.LastErrorMessage appears)

## Files to Modify/Create

| File | Change | Priority |
|------|--------|----------|
| `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs` | NEW - implements pull logic, uses IEventAndConfigService | 1 |
| `src/client/host/VideoForensics.Hosting/Services/EventPullService.Tests.cs` | NEW - xUnit tests with Moq | 2 |
| `src/core/api/VideoForensics.Api.Contracts/AccountDtos.cs` | Add `SyncNowResponseDto` | 3 |
| `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs` | Fire background pull after add, add POST /sync-now endpoint | 4 |
| `src/client/ui/VideoForensics.Ui.Shared/Pages/AccountDetails.razor` | Add "Sync Now" button, display pull status/errors | 5 |

**Note:** ProviderAccount already has `LastSuccessfulAuthUtc` and `LastErrorMessage` fields—no schema changes needed.

## Verification

**Lite gate** (after each file completed):
- `dotnet build` on EventPullService.csproj, VideoForensics.Hosting.csproj, VideoForensics.WebApp.csproj, VideoForensics.Ui.Shared.csproj
- `dotnet test` on EventPullService sibling tests project

**End-to-end verification** (once all files complete):
1. Start dev server (WebApp + UI)
2. Link a new provider account → verify background pull fires (check ProviderAccount.LastSuccessfulAuthUtc after ~5s)
3. Navigate to AccountDetails for that account
4. Click "Sync Now" button → verify:
   - Spinner appears
   - Toast shows "Sync started" or success message
   - ProviderAccount.LastSuccessfulAuthUtc updates
5. Simulate failure: modify EventPullService to throw error → Click "Sync Now" → verify error toast and error message displays
6. Verify "Sync Now" button re-enables after pull completes (success or failure)

## Dependencies
- Phase 1 must be complete (provider binding, account linking flow)
- Existing IEventAndConfigService implementations (Ring, Wyze providers)
- Data layer with ProviderAccount entity

## Next Steps
Phase 3: Project reorganization (src/utils folder restructuring)

---

**Status:** Plan complete. Ready for implementation. Delegation order: EventPullService → tests → DTOs → AccountEndpoints → AccountDetails.razor
