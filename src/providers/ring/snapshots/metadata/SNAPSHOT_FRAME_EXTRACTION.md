# Snapshot Frame Extraction for Domestic Violence Evidence

This document describes the snapshot frame extraction capabilities, designed to support domestic violence (DV) victims in documenting evidence through Ring snapshot events.

## Overview

The snapshot frame extraction system downloads Ring snapshots directly from event URLs and correlates them with comprehensive metadata about detections, anomalies, and threats at that moment. Unlike video frame extraction, snapshot frames are downloaded directly from Ring's servers in their native format.

### Key Features

- **Direct Snapshot Download**: Downloads snapshots from Ring-provided URLs
- **Full Metadata Tagging**: Snapshots tagged with detection type, confidence, anomaly scores, profiles
- **Evidence Summary Generation**: Creates human-readable text reports alongside snapshots
- **Platform Agnostic**: Works on Windows, Linux, and macOS
- **Image Format Detection**: Automatically detects JPEG, PNG, WebP, and GIF formats

## Architecture

### SnapshotFrameExtractor (`SnapshotFrameExtractor.cs`)

The main implementation for downloading and processing snapshots.

```csharp
// Download and tag a snapshot
var extractor = new SnapshotFrameExtractor();
var processed = extractor.DownloadAndTagSnapshot(
    snapshotUrl,      // Ring-provided snapshot URL
    metadata,          // SnapshotMetadata with detection info
    outputDir          // Where to save the snapshot
);

// Generate evidence summary document
var summaryPath = extractor.GenerateEvidenceSummary(
    processed,
    metadata,
    outputDir
);
```

### ISnapshotFrameExtractor Interface (`ISnapshotFrameExtractor.cs`)

Defines the snapshot frame extraction contract.

**Key Methods:**
- `DownloadAndTagSnapshotAsync(url, metadata, outputDir)` - Download and tag snapshot
- `DownloadAndTagSnapshot(...)` - Synchronous version
- `GenerateEvidenceSummaryAsync(snapshot, metadata, outputDir)` - Create summary document

### ProcessedSnapshot DTO

Each downloaded snapshot includes:

```csharp
public class ProcessedSnapshot
{
    // URL and Timing Information
    public string? SnapshotUrl { get; set; }                // Original Ring URL
    public long TimestampMs { get; set; }                   // Epoch milliseconds
    public string? TimeFormatted { get; set; }              // yyyy-MM-dd_HH-mm-ss format
    
    // File Information
    public string? FileName { get; set; }                   // Local filename
    public string? FilePath { get; set; }                   // Full path to snapshot
    public long FileSizeBytes { get; set; }                 // File size
    public string? ImageFormat { get; set; }                // JPEG, PNG, WebP, GIF
    public string? Dimensions { get; set; }                 // Width x height
    
    // Detection Information
    public string? DetectionType { get; set; }              // person, motion, vehicle, etc.
    public double? DetectionConfidence { get; set; }        // 0.0-1.0 confidence score
    public double? AnomalyScore { get; set; }               // 0.0-1.0 anomaly score
    
    // Critical DV Evidence Fields
    public List<DetectedProfile>? RecognizedProfiles { get; set; }  // Identified people
    public List<string>? SecurityAlerts { get; set; }               // Glass breaking, loud noise, etc.
    public string? AlertSeverity { get; set; }                      // Alert severity level
    public List<MotionZone>? ActiveZones { get; set; }              // Where in frame activity occurred
    
    // Status Information
    public bool ProcessingSuccessful { get; set; }          // Success indicator
    public string? ProcessingError { get; set; }            // Error message if failed
    public DateTime ProcessedAt { get; set; }               // When downloaded
    
    // Evidence Documentation
    public string? EvidenceSummaryPath { get; set; }        // Path to generated summary
}
```

## Usage Examples

### Basic Snapshot Download

```csharp
var extractor = new SnapshotFrameExtractor();

// Download snapshot and tag with metadata
var snapshot = extractor.DownloadAndTagSnapshot(
    snapshotUrl: "https://...",
    metadata: snapshotMetadata,
    outputDirectory: "/evidence/snapshots"
);

// Check download status
if (snapshot?.ProcessingSuccessful ?? false)
{
    Console.WriteLine($"Snapshot saved: {snapshot.FilePath}");
    Console.WriteLine($"Size: {snapshot.FileSizeBytes} bytes");
    Console.WriteLine($"Format: {snapshot.ImageFormat}");
    Console.WriteLine($"Detection: {snapshot.DetectionType}");
    
    if (snapshot.RecognizedProfiles?.Count > 0)
    {
        foreach (var profile in snapshot.RecognizedProfiles)
        {
            Console.WriteLine($"  👤 {profile.Name} ({profile.Confidence * 100}%)");
        }
    }
}
else
{
    Console.WriteLine($"⚠️ Download failed: {snapshot?.ProcessingError}");
}
```

### Async Snapshot Download

```csharp
var extractor = new SnapshotFrameExtractor();

// Async download
var snapshot = await extractor.DownloadAndTagSnapshotAsync(
    snapshotUrl: "https://...",
    metadata: snapshotMetadata,
    outputDirectory: "/evidence/snapshots"
);
```

### Generate Evidence Summary

```csharp
var extractor = new SnapshotFrameExtractor();

// Download snapshot
var snapshot = extractor.DownloadAndTagSnapshot(
    snapshotUrl,
    metadata,
    outputDir
);

// Generate human-readable evidence summary
if (snapshot?.ProcessingSuccessful ?? false)
{
    var summaryPath = extractor.GenerateEvidenceSummary(
        snapshot,
        metadata,
        outputDir
    );
    
    // Summary file contains:
    // - Event timestamp and ID
    // - Device information and status
    // - Location (address and coordinates)
    // - Snapshot metadata (format, size, dimensions)
    // - Detection information and confidence
    // - Recognized profiles with confidence
    // - Security alerts and severity
    // - Motion zones
    // - Device health (signal, battery)
}
```

### Custom HTTP Client

```csharp
// Use custom HTTP client with proxy or authentication
var httpClient = new HttpClient(new HttpClientHandler
{
    Proxy = new WebProxy("http://proxy.example.com:8080"),
    UseProxy = true
});

var extractor = new SnapshotFrameExtractor(
    fileSystem: new FileSystem(),
    httpClient: httpClient
);

var snapshot = extractor.DownloadAndTagSnapshot(
    snapshotUrl,
    metadata,
    outputDir
);
```

## Snapshot Download Process

1. **Validate Input**: Check snapshot URL and metadata are valid
2. **Create Output Directory**: Ensure target directory exists
3. **Download Snapshot**: Fetch from Ring URL via HTTP
4. **Validate HTTP Response**: Check for successful download (HTTP 200)
5. **Write to Disk**: Save snapshot file to output directory
6. **Detect Format**: Analyze file headers to identify image format
7. **Create Metadata**: Package snapshot info with Ring metadata
8. **Tag Information**: Associate all detection data with snapshot

## Domestic Violence Evidence Use Cases

### 1. Single-Frame Evidence

Snapshots capture a single moment of activity with full Ring AI analysis:

```csharp
// Snapshot at exact moment of detection
Console.WriteLine($"Evidence Moment: {snapshot.TimeFormatted}");
Console.WriteLine($"Detection Type: {snapshot.DetectionType}");
Console.WriteLine($"Confidence: {snapshot.DetectionConfidence * 100}%");

if (snapshot.SecurityAlerts?.Count > 0)
{
    Console.WriteLine($"ALERTS: {string.Join(", ", snapshot.SecurityAlerts)}");
}
```

### 2. Perpetrator Identification

Face recognition profiles are automatically included:

```csharp
foreach (var profile in snapshot.RecognizedProfiles ?? new List<DetectedProfile>())
{
    Console.WriteLine($"Identified: {profile.Name}");
    Console.WriteLine($"Confidence: {profile.Confidence * 100}%");
    
    if (!string.IsNullOrEmpty(profile.ThumbnailUrl))
    {
        Console.WriteLine($"Thumbnail: {profile.ThumbnailUrl}");
    }
}
```

### 3. Anomaly Detection

Anomaly scores indicate suspicious or unusual activity:

```csharp
if (snapshot.AnomalyScore > 0.8)
{
    Console.WriteLine($"⚠️ HIGH ANOMALY SCORE: {snapshot.AnomalyScore}");
    Console.WriteLine("Indicates unusual/suspicious activity detected");
}
```

### 4. Zone-Based Evidence

Motion zones show where in camera's view activity occurred:

```csharp
if (snapshot.ActiveZones?.Count > 0)
{
    Console.WriteLine("Activity Zones:");
    foreach (var zone in snapshot.ActiveZones)
    {
        Console.WriteLine($"  - {zone.Name}: {zone.Confidence * 100}% confidence");
    }
}
```

## Evidence Documentation

### Generated Summary Format

The evidence summary document includes:

```
=== RING EVENT EVIDENCE SUMMARY ===

EVENT INFORMATION:
  Timestamp: 2026-08-21_14-32-45 (UTC)
  Epoch (ms): 1724256765000
  Event ID: abc123def456
  Event Kind: motion

DEVICE INFORMATION:
  Name: Front Door
  Manufacturer: Amazon
  Model: Doorbell
  Firmware: 1.8.26
  Online: True
  Notifications Enabled: True

LOCATION INFORMATION:
  Address: 123 Main St, Springfield, IL 62701
  Coordinates: 39.761111, -89.650000
  Timezone: America/Chicago

SNAPSHOT INFORMATION:
  File: snapshot_2026-08-21_14-32-45.jpg
  Path: /evidence/snapshots/snapshot_2026-08-21_14-32-45.jpg
  Size: 245.50 KB
  Format: JPEG
  Dimensions: 1920x1080

DETECTION INFORMATION:
  Detection Type: person
  Confidence: 98.50%
  Anomaly Score: 0.45%

RECOGNIZED PROFILES:
  - John Doe (Confidence: 95.00%)
    ID: profile_john_doe_123
  - Jane Smith (Confidence: 87.50%)
    ID: profile_jane_smith_456

SECURITY ALERTS:
  Severity: HIGH
  - Loud noise detected
  - Aggressive behavior pattern

MOTION ZONES:
  - front door (Confidence: 99.00%)
  - entry hall (Confidence: 92.50%)

DEVICE HEALTH:
  Signal (RSSI): -55 dBm
  Battery: 85%
```

## Image Format Support

Automatically detects and documents various image formats:

| Format | Signature | Detection |
|--------|-----------|-----------|
| JPEG | FF D8 FF | ✅ Supported |
| PNG | 89 50 4E 47 | ✅ Supported |
| WebP | 52 49 46 46 ... 57 45 42 50 | ✅ Supported |
| GIF | 47 49 46 | ✅ Supported |

## Error Handling

Snapshot download includes comprehensive error handling:

```csharp
if (!snapshot?.ProcessingSuccessful ?? true)
{
    // snapshot.ProcessingError contains:
    // - HTTP status code and reason
    // - File write errors
    // - Network timeouts
    // - Invalid metadata
    
    Console.WriteLine($"Download Error: {snapshot?.ProcessingError}");
}
```

### Common Error Scenarios

- **HTTP 404**: Snapshot URL no longer valid (Ring may purge old snapshots)
- **HTTP 403**: Unauthorized access to snapshot (authentication issue)
- **File I/O**: Output directory not writable
- **Network**: Timeout or connection refused

## Snapshot URL Lifecycle

Ring snapshots follow Ring's retention policies:

```csharp
// Download immediately or risk URL expiration
var snapshot = extractor.DownloadAndTagSnapshot(
    snapshotUrl,     // May expire after few days
    metadata,
    outputDir
);

// Snapshots saved locally remain available permanently
// Original URL may no longer work after Ring purges old data
```

## Platform Compatibility

Snapshot frame extraction is **completely platform-agnostic**:

| Component | Windows | Linux | macOS |
|-----------|---------|-------|-------|
| HTTP Client | ✅ HttpClient | ✅ HttpClient | ✅ HttpClient |
| File I/O | ✅ via IFileSystem | ✅ via IFileSystem | ✅ via IFileSystem |
| Image Format Detection | ✅ Binary headers | ✅ Binary headers | ✅ Binary headers |

No platform-specific code paths exist in `SnapshotFrameExtractor.cs`.

## Performance Considerations

- **Direct Download**: Snapshots downloaded directly from Ring (no re-encoding)
- **Format Detection**: Binary header analysis (12 bytes) is very fast
- **Summary Generation**: Text file generation is lightweight
- **Async Support**: Downloads can be awaited without blocking

## Security Considerations

### URL Validation

```csharp
// Must be HTTPS or HTTP URL
if (!Uri.TryCreate(snapshotUrl, UriKind.Absolute, out var uri))
    return null;

if (uri.Scheme != "http" && uri.Scheme != "https")
    return null;
```

### File Permissions

- HttpClient runs under same user permissions as application
- Output directory must be writable
- Uses IFileSystem abstraction to prevent path traversal

### Evidence Chain of Custody

Each snapshot records:
- Original Ring URL (for verification)
- Download timestamp (ProcessedAt)
- File path and size (for validation)
- Processing status and any errors

## Testing

Snapshot frame extraction includes comprehensive unit tests:

```bash
# Run snapshot metadata tests
dotnet test src/snapshots/metadata/tests/
```

Test scenarios cover:
- Successful snapshot download and tagging
- HTTP error handling
- File write errors
- Image format detection
- Summary document generation
- Platform compatibility

## Integration with Metadata Pipeline

Snapshot extraction integrates naturally into the metadata pipeline:

```csharp
// 1. Extract snapshot metadata from Ring event
var extractor = new SnapshotMetadataExtractor();
var metadata = extractor.ExtractMetadata(ringEvent);

// 2. Download and tag snapshot
var frameExtractor = new SnapshotFrameExtractor();
var snapshot = frameExtractor.DownloadAndTagSnapshot(
    ringEvent.SnapshotUrl,
    metadata,
    outputDir
);

// 3. Generate evidence summary
if (snapshot?.ProcessingSuccessful ?? false)
{
    var summaryPath = frameExtractor.GenerateEvidenceSummary(
        snapshot,
        metadata,
        outputDir
    );
}
```

## Related Components

- **IVideoFrameExtractor**: Extracts frames from videos at timestamps
- **IVideoThumbnailExtractor**: Associates snapshots as video thumbnails
- **SnapshotMetadata**: Full metadata model with image-specific fields
- **DetectedProfile**: Face recognition data for perpetrator identification
- **SecurityAlerts**: Anomaly detection and threat identification

## References

- **Ring API Snapshots**: Ring.Api.Snapshots
- **Ring Metadata Models**: Ring.Api.Snapshots.Metadata.Models
- **System.IO.Abstractions**: Platform-agnostic file operations
- **HttpClient**: .NET HttpClient for network operations
