# Wyze Provider Implementation Guide

This is a **placeholder implementation** of the Wyze provider for VideoForensics. It follows the exact same architecture pattern as the Ring provider and demonstrates how to add support for any new video provider.

## Structure

```
VideoForensics.Providers.Wyze/
├── WyzeVideoProvider.cs                 # Main provider orchestrator
└── Services/
    ├── WyzeAuthService.cs               # Authentication/credential management
    ├── WyzeDeviceDiscoveryService.cs    # Device and location enumeration
    ├── WyzeMediaDownloadService.cs      # Video/snapshot downloading
    └── WyzeEventAndConfigService.cs     # Events and device settings
```

## Implementation Status

| Component | Status | Notes |
|-----------|--------|-------|
| WyzeVideoProvider | ✅ Scaffolding | Ready for Wyze API integration |
| WyzeAuthService | 🔄 Placeholder | Implement Wyze OAuth/API authentication |
| WyzeDeviceDiscoveryService | 🔄 Placeholder | Implement device/location enumeration |
| WyzeMediaDownloadService | 🔄 Placeholder | Implement video/snapshot download |
| WyzeEventAndConfigService | 🔄 Placeholder | Implement event retrieval and device config |
| Tests | ✅ Complete | 4 tests verifying interface compliance |

## How to Implement

### 1. Authentication (WyzeAuthService)

Implement OAuth 2.0 or API key authentication with Wyze:

```csharp
public override async Task<AuthResult> AuthenticateAsync(string username, string password)
{
    // TODO: Implement these steps:
    // 1. Initialize Wyze SDK or HTTP client
    // 2. Call Wyze login endpoint
    // 3. Handle MFA challenges if required
    // 4. Extract and cache access token
    // 5. Store refresh token for token lifecycle management
    // 6. Return AuthResult with token and expiration
}
```

**Key Considerations:**
- Wyze may require app key/secret in addition to user credentials
- Handle rate limiting and retry logic
- Support MFA (two-factor authentication)
- Cache tokens to minimize API calls

### 2. Device Discovery (WyzeDeviceDiscoveryService)

Enumerate Wyze devices and locations:

```csharp
public override async Task<IReadOnlyList<Location>> GetLocationsAsync()
{
    // TODO: Implement these steps:
    // 1. Call Wyze API to get user's homes/properties
    // 2. Map each home to Location DTO
    // 3. Include address, timezone, and metadata
    // 4. Return read-only list
}

public override async Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId)
{
    // TODO: Implement these steps:
    // 1. Call Wyze API with location/home ID
    // 2. Get all camera devices at this location
    // 3. Handle multiple device types (Wyze Cam, Wyze Video Doorbell, etc)
    // 4. Map to Device DTOs with online/offline status
    // 5. Return read-only list
}
```

**Key Considerations:**
- Wyze supports cameras, doorbells, contact sensors, motion sensors, etc.
- Devices may have firmware versions and capabilities metadata
- Cache device lists and refresh on-demand or periodically

### 3. Media Download (WyzeMediaDownloadService)

Implement video and snapshot retrieval:

```csharp
public override async Task<DownloadResult> DownloadVideosAsync(
    string deviceId, string outputPath, DateTime startDate, DateTime endDate, 
    CancellationToken cancellationToken = default)
{
    // TODO: Implement these steps:
    // 1. Query Wyze API for events/videos in date range
    // 2. Get signed download URLs from Wyze cloud
    // 3. Stream each video to local file
    // 4. Verify integrity (file size, checksum if available)
    // 5. Track progress via _currentStatus
    // 6. Return DownloadResult with success/failure
}
```

**Key Considerations:**
- Wyze videos are cloud-hosted; may require download tokens/URLs
- Handle bandwidth throttling and connection failures
- Maintain consistent file naming for forensic integrity
- Provide progress tracking for long operations

### 4. Events & Configuration (WyzeEventAndConfigService)

Retrieve device events and settings:

```csharp
public override async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
    string deviceId, DateTime startDate, DateTime endDate, string? eventType = null)
{
    // TODO: Implement these steps:
    // 1. Query Wyze API for events (motion, person detection, sound)
    // 2. Filter by eventType if specified
    // 3. Map to DeviceEvent DTOs
    // 4. Include snapshot URLs for forensic analysis
    // 5. Return chronologically ordered list
}

public override async Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId)
{
    // TODO: Implement device configuration retrieval
    // Include: motion detection, sensitivity, recording mode, etc.
}

public override async Task<bool> UpdateDeviceConfigAsync(string deviceId, DeviceConfig config)
{
    // TODO: Implement device configuration updates
    // Audit changes for chain of custody
}
```

**Key Considerations:**
- Event types: motion, person detection, sound, alarm, etc.
- Sensitivity levels vary by device type
- Configuration changes should be logged for forensic integrity
- Some settings may be read-only depending on device/subscription

## Testing

The placeholder includes 4 integration tests:

```bash
dotnet test VideoForensics.Providers.Wyze.Tests/
```

Tests verify:
- ✅ Provider name is "Wyze"
- ✅ Implements IVideoProvider interface
- ✅ All four required services are instantiated
- ✅ Services implement their interfaces

Add more tests as you implement each service:
- Mock Wyze API responses
- Test error handling and edge cases
- Verify data mapping to platform-agnostic DTOs

## Integration with VideoForensics

Once implemented, the Wyze provider is automatically compatible with VideoForensics:

```csharp
// Consumer code doesn't know it's using Wyze
IVideoProvider provider = new WyzeVideoProvider(logger);

// Same interface works for Ring, Wyze, or any other provider
var devices = await provider.DeviceService.GetAllDevicesAsync();
var videos = await provider.DownloadService.DownloadVideosAsync(deviceId, path, start, end);
var events = await provider.EventService.GetEventsAsync(deviceId, start, end);
```

## Wyze API Resources

- **Wyze Developer Portal:** https://developer.wyze.com
- **API Documentation:** https://developer.wyze.com/docs
- **OAuth Guide:** Check Wyze docs for authentication flow
- **Rate Limits:** Typically 20-30 requests/minute; implement backoff
- **Error Codes:** Document device-specific limitations

## Security Considerations

1. **Credentials:** Store in secure config, never in code
2. **Tokens:** Cache with expiration; implement refresh
3. **URLs:** Don't log signed download URLs; they contain auth tokens
4. **Validation:** Verify file integrity before storing
5. **Audit Trail:** Log all API calls for forensic integrity

## Performance Optimization

- **Batch Operations:** Use bulk endpoints if available
- **Caching:** Cache device lists, refresh every 5-30 minutes
- **Async/Await:** All I/O must be async with cancellation support
- **Streaming:** Stream large videos; don't buffer to memory
- **Connection Pooling:** Reuse HTTP clients across requests

## Next Steps

1. Register with Wyze Developer Platform and get API credentials
2. Implement WyzeAuthService (start with OAuth/API key auth)
3. Add integration tests with mock Wyze API responses
4. Implement WyzeDeviceDiscoveryService
5. Implement WyzeMediaDownloadService with progress tracking
6. Implement WyzeEventAndConfigService
7. Add end-to-end tests with real Wyze account (if available)
8. Document any Wyze-specific limitations or quirks

## Architecture Benefits

By following the IVideoProvider pattern, Wyze support:
- ✅ Works with existing VideoForensics.Core business logic
- ✅ Reuses forensics library (VideoForensics.Forensics)
- ✅ Supports multi-provider scenarios (Ring + Wyze in same app)
- ✅ Follows CLAUDE.md architectural guidelines
- ✅ Maintains platform-agnostic interfaces
- ✅ Enables future providers (Blue Iris, Ubiquiti, etc.)
