# Phase 2: Automatic Event/Snapshot Pull on Account Connect

## Scope
Implement automatic data pull when users connect provider accounts, with manual refresh button.

## Changes by Item

### Item 2: Automatic Event/Snapshot Pull + Manual Refresh

**Strategy:**
- On successful account auth → trigger background pull of events since last successful pull
- Track `ProviderAccount.LastSuccessfulAuthUtc` (pull from this timestamp forward)
- First-time connect: pull all available data
- Add "Sync Now" button to AccountDetails page
- Pull is non-blocking (fire-and-forget with error notification)
- Also pulls device config on account connect

**Files to modify/create:**

1. **Data Model:**
   - Modify: `ProviderAccount` entity
     - Add/verify field: `LastSuccessfulAuthUtc` (nullable DateTime)
     - Add/verify field: `LastErrorMessage` (string, nullable)

2. **Business Logic:**
   - New: `src/client/host/VideoForensics.Hosting/Services/EventPullService.cs`
     - `PullAccountEventsAsync(accountId, fromTimestampUtc, cancellationToken)`
     - If fromTimestampUtc is null: pull all available data
     - If fromTimestampUtc is set: pull from that timestamp onward
     - Calls IEventAndConfigService.GetEventsAsync for all devices
     - Also calls GetDeviceConfigAsync for each device
     - Stores errors in ProviderAccount.LastErrorMessage
     - Records success in ProviderAccount.LastSuccessfulAuthUtc

3. **API Endpoints:**
   - Modify: `src/client/web/VideoForensics.WebApp/Api/AccountEndpoints.cs`
     - After successful POST /api/v1/accounts/add: call `EventPullService.PullAccountEventsAsync()`
       - Parameters: account ID, pull from LastSuccessfulAuthUtc (or null for first-time)
   
   - New endpoint: `POST /api/v1/accounts/{accountId}/sync-now`
     - Calls EventPullService.PullAccountEventsAsync
     - Returns 200 on success, error code if pull fails
     - Response includes pull status and any error message

4. **UI:**
   - Modify: `src/client/ui/VideoForensics.Ui.Shared/Pages/AccountDetails.razor`
     - Add "Sync Now" button next to account name (or in action bar)
     - On click: POST to `POST /api/v1/accounts/{id}/sync-now`
     - Show spinner during pull
     - Toast notification on completion or error
     - Display last pull timestamp and status

## Testing Checklist

- [ ] EventPullService compiles and has xUnit tests
  - [ ] Test: pull all data on first-time connect (fromTimestampUtc = null)
  - [ ] Test: pull data since last auth timestamp
  - [ ] Test: error handling when pull fails
  - [ ] Test: LastSuccessfulAuthUtc is updated on success
  - [ ] Test: LastErrorMessage is set on failure

- [ ] AccountEndpoints compiles and integrates correctly
  - [ ] POST /api/v1/accounts/add triggers auto-pull
  - [ ] POST /api/v1/accounts/{id}/sync-now responds with pull status

- [ ] UI integration
  - [ ] AccountDetails.razor loads without errors
  - [ ] "Sync Now" button visible and clickable
  - [ ] Clicking button shows spinner and calls sync endpoint
  - [ ] Toast appears on success/error
  - [ ] No UI blocking during pull (async/await pattern)

- [ ] End-to-end
  - [ ] Link new provider account → events/config auto-pulled
  - [ ] Click "Sync Now" → events/config refreshed
  - [ ] Failed pull shows error in AccountDetails
  - [ ] Timestamps update correctly in ProviderAccount record

## Dependencies
- Phase 1 must be complete (if deploying together)

## Next Phase
Phase 3: Project Reorganization (src/utils folder restructuring)
