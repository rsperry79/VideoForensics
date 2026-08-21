# Video Frame Extraction for Domestic Violence Evidence

This document describes the frame extraction capabilities for Ring video metadata, designed to support domestic violence (DV) victims in documenting evidence with precise visual timelines.

## Overview

The frame extraction system uses **FFmpeg** to extract individual video frames at verified detection timestamps, creating a visual evidence timeline from Ring video events. Each extracted frame is automatically tagged with comprehensive metadata about detections, anomalies, and threats at that moment.

### Key Features

- **Precise Timestamp Extraction**: Frames extracted at exact detection moments (verified by Ring AI)
- **Full Metadata Tagging**: Every frame includes detection type, confidence, anomaly scores, recognized profiles, and security alerts
- **Platform Agnostic**: Works on Windows, Linux, and macOS via FFmpeg
- **Evidence Timeline**: Creates visual proof of when suspicious activity was detected
- **No Re-encoding**: Uses FFmpeg's fast seek to extract frames without degrading video quality

## Architecture

### VideoFrameExtractor (`VideoFrameExtractor.cs`)

The main implementation for extracting frames from Ring videos.

```csharp
// Extract frames at verified detection timestamps
var extractor = new VideoFrameExtractor();
var frames = extractor.ExtractDetectionFrames(
    videoPath,
    metadata,        // VideoMetadata with VerifiedDetectionTimestamps
    outputDir
);

// Or extract at specific timestamps
var frames = extractor.ExtractFramesAtTimestamps(
    videoPath,
    new List<long> { 1000, 2000, 3000 },  // epoch milliseconds
    outputDir
);
```

### IVideoFrameExtractor Interface (`IVideoFrameExtractor.cs`)

Defines the frame extraction contract.

**Key Methods:**
- `ExtractDetectionFramesAsync(videoPath, metadata, outputDir)` - Extract at verified timestamps
- `ExtractFramesAtTimestampsAsync(videoPath, timestamps, outputDir)` - Extract at specific times
- `ExtractDetectionFrames(...)` - Synchronous versions

### ExtractedFrame DTO

Each extracted frame includes:

```csharp
public class ExtractedFrame
{
    // Timing Information
    public long TimestampMs { get; set; }                  // Epoch milliseconds
    public string TimeFormatted { get; set; }              // HH:MM:SS.mmm format
    
    // File Information
    public string FrameFileName { get; set; }              // Local filename (frame_HH-MM-SS_mmm.jpg)
    public string FrameFilePath { get; set; }              // Full path to frame
    public long FileSizeBytes { get; set; }                // Frame file size
    
    // Detection Information
    public string DetectionType { get; set; }              // person, motion, vehicle, etc.
    public double? DetectionConfidence { get; set; }       // 0.0-1.0 confidence score
    public double? AnomalyScore { get; set; }              // 0.0-1.0 anomaly score
    
    // Critical DV Evidence Fields
    public List<DetectedProfile>? RecognizedProfiles { get; set; }  // Identified people
    public List<string>? SecurityAlerts { get; set; }              // Glass breaking, loud noise, etc.
    public List<MotionZone>? ActiveZones { get; set; }             // Where in frame activity occurred
    
    // Status Information
    public bool ExtractionSuccessful { get; set; }         // Success indicator
    public string? ExtractionError { get; set; }           // Error message if failed
    public DateTime ExtractedAt { get; set; }              // When extraction occurred
}
```

## Usage Examples

### Basic Frame Extraction

```csharp
var extractor = new VideoFrameExtractor();

// Extract frames at verified detection timestamps
var frames = extractor.ExtractDetectionFrames(
    videoFilePath: "/path/to/video.mp4",
    metadata: videoMetadata,
    outputDirectory: "/evidence/frames"
);

// Check results
foreach (var frame in frames)
{
    if (frame.ExtractionSuccessful)
    {
        Console.WriteLine($"Frame extracted: {frame.FrameFilePath}");
        Console.WriteLine($"  Time: {frame.TimeFormatted}");
        Console.WriteLine($"  Detection: {frame.DetectionType} ({frame.DetectionConfidence}%)");
        
        if (frame.SecurityAlerts?.Count > 0)
        {
            Console.WriteLine($"  ⚠️ Alerts: {string.Join(", ", frame.SecurityAlerts)}");
        }
        
        if (frame.RecognizedProfiles?.Count > 0)
        {
            foreach (var profile in frame.RecognizedProfiles)
            {
                Console.WriteLine($"  👤 {profile.Name} ({profile.Confidence}%)");
            }
        }
    }
    else
    {
        Console.WriteLine($"⚠️ Extraction failed: {frame.ExtractionError}");
    }
}
```

### Async Frame Extraction

```csharp
var extractor = new VideoFrameExtractor();

// Async extraction
var frames = await extractor.ExtractDetectionFramesAsync(
    videoFilePath: "/path/to/video.mp4",
    metadata: videoMetadata,
    outputDirectory: "/evidence/frames"
);
```

### Custom Timestamp Extraction

```csharp
var extractor = new VideoFrameExtractor();

// Extract at specific timestamps (epoch milliseconds)
var customTimestamps = new List<long>
{
    5000,   // 5 seconds
    10000,  // 10 seconds
    15000   // 15 seconds
};

var frames = extractor.ExtractFramesAtTimestamps(
    videoFilePath: "/path/to/video.mp4",
    timestamps: customTimestamps,
    outputDirectory: "/evidence/frames"
);
```

### Custom FFmpeg Path

```csharp
// Use specific FFmpeg installation
var extractor = new VideoFrameExtractor(
    fileSystem: new FileSystem(),
    ffmpegPath: "/usr/local/bin/ffmpeg"  // or "C:\\ffmpeg\\ffmpeg.exe" on Windows
);

var frames = extractor.ExtractDetectionFrames(videoPath, metadata, outputDir);
```

## FFmpeg Integration

### Requirements

FFmpeg must be installed and available in PATH:

**Windows:**
```bash
# Via Chocolatey
choco install ffmpeg

# Via direct download
# https://ffmpeg.org/download.html
```

**Linux:**
```bash
# Ubuntu/Debian
sudo apt-get install ffmpeg

# Fedora/RHEL
sudo yum install ffmpeg
```

**macOS:**
```bash
# Via Homebrew
brew install ffmpeg
```

### Frame Extraction Process

1. **Seek to Timestamp**: FFmpeg uses `-ss` flag for fast timestamp seeking
2. **Extract Single Frame**: `-vframes 1` ensures exactly one frame per timestamp
3. **Output as JPEG**: `-f image2` outputs to image format
4. **30-Second Timeout**: Each frame extraction has a 30-second timeout
5. **Metadata Tagging**: Ring metadata automatically attached to each frame

### FFmpeg Command Example

```bash
ffmpeg -ss 00:00:05.123 -i video.mp4 -vframes 1 -f image2 frame_00-00-05_123.jpg
```

## Domestic Violence Evidence Use Cases

### 1. Establishing Attack Timeline

Extract frames at verified detection moments to create a visual timeline:

```
Frame at 14:32:45.123 - Person detected (98% confidence)
Frame at 14:32:52.456 - Motion in "entry hall" zone
Frame at 14:33:01.789 - Anomaly score spike (0.87)
                        Loud noise alert detected
```

### 2. Identifying Perpetrators and Witnesses

Recognized face profiles are automatically included:

```csharp
var profile = frame.RecognizedProfiles[0];
// profile.Name = "John Doe"
// profile.Confidence = 0.95
// profile.ThumbnailUrl = "https://..."
```

### 3. Detecting Tampering/Device Failures

Frame extraction captures anomaly scores indicating suspicious activity:

```csharp
if (frame.AnomalyScore > 0.8)
{
    // Unusual/suspicious activity detected
    // Could indicate violence, disturbance, or device tampering
}
```

### 4. Zone-Based Evidence

Motion zones show where in the camera's view activity occurred:

```csharp
var zone = frame.ActiveZones[0];
// zone.Name = "front door"
// zone.Confidence = 0.92
```

## Evidence Documentation

### Creating Evidence Reports

Frame extraction supports creating detailed evidence timelines for law enforcement:

```csharp
// For each frame
Console.WriteLine($"=== EVIDENCE FRAME ===");
Console.WriteLine($"Time: {frame.TimeFormatted}");
Console.WriteLine($"Detection: {frame.DetectionType}");
Console.WriteLine($"Confidence: {frame.DetectionConfidence}");
Console.WriteLine($"Anomaly Score: {frame.AnomalyScore}");

if (frame.SecurityAlerts != null)
{
    Console.WriteLine($"ALERTS: {string.Join(", ", frame.SecurityAlerts)}");
}

if (frame.RecognizedProfiles != null)
{
    Console.WriteLine($"IDENTIFIED: {string.Join(", ", frame.RecognizedProfiles.Select(p => p.Name))}");
}

Console.WriteLine($"Frame File: {frame.FrameFilePath}");
Console.WriteLine($"File Size: {frame.FileSizeBytes} bytes");
Console.WriteLine($"---");
```

### Chain of Custody

Each frame records:
- Exact extraction timestamp (ProcessedAt)
- Frame extraction status
- Any extraction errors (indicates potential tampering)

## Platform Compatibility

Frame extraction is **completely platform-agnostic**:

| Component | Windows | Linux | macOS |
|-----------|---------|-------|-------|
| FFmpeg Execution | ✅ via Process | ✅ via Process | ✅ via Process |
| File I/O | ✅ via IFileSystem | ✅ via IFileSystem | ✅ via IFileSystem |
| Timestamp Formatting | ✅ DateTime | ✅ DateTime | ✅ DateTime |

No platform-specific code paths exist in `VideoFrameExtractor.cs`.

## Error Handling

Frame extraction includes comprehensive error handling:

```csharp
if (!frame.ExtractionSuccessful)
{
    // frame.ExtractionError contains:
    // - FFmpeg process errors
    // - File system errors
    // - Timeout errors
    // - Invalid video path errors
    
    switch (frame.ExtractionError)
    {
        case string s when s.Contains("Invalid"):
            // Handle invalid video file
            break;
        case string s when s.Contains("timeout"):
            // Handle timeout (may indicate large frame or slow system)
            break;
        default:
            // Log and continue
            break;
    }
}
```

## Performance Considerations

- **Timestamp Seeking**: FFmpeg's `-ss` flag is much faster than frame-by-frame decoding
- **JPEG Output**: Single-frame JPEG extraction is lightweight
- **30-Second Timeout**: Allows time for FFmpeg startup and frame extraction
- **Async Operations**: Frame extraction can be awaited without blocking

## Security Considerations

### Input Validation

```csharp
// Video file must exist
if (!File.Exists(videoPath))
    return null;

// Timestamps must be reasonable
foreach (var ts in timestamps)
{
    if (ts < 0 || ts > int.MaxValue)
        return null;
}
```

### File Permissions

- FFmpeg runs under same user permissions as application
- Output directory must be writable
- Uses IFileSystem abstraction to prevent path traversal attacks

## Testing

Frame extraction includes comprehensive unit tests:

```bash
# Run video metadata tests including frame extraction
dotnet test src/video/metadata/tests/
```

Test scenarios cover:
- Successful frame extraction at multiple timestamps
- Error handling (missing video, FFmpeg not installed)
- Timestamp formatting
- Metadata tagging
- Platform compatibility

## Integration with Metadata Pipeline

Frame extraction integrates naturally into the metadata pipeline:

```csharp
// 1. Extract video metadata from Ring event
var extractor = new MetadataExtractor();
var metadata = extractor.ExtractMetadata(ringEvent);

// 2. Extract frames at verified detection times
var frameExtractor = new VideoFrameExtractor();
var frames = frameExtractor.ExtractDetectionFrames(
    videoPath,
    metadata,
    outputDir
);

// 3. Write metadata to video file
var writer = new NoOpMetadataWriter();
var result = writer.WriteMetadata(metadata, videoPath);
```

## Related Components

- **ISnapshotFrameExtractor**: Downloads and processes snapshot thumbnails
- **IVideoThumbnailExtractor**: Associates snapshots as video thumbnails
- **VerifiedDetectionTimestamps**: Critical field in VideoMetadata for frame extraction
- **DetectedProfile**: Face recognition data for perpetrator identification
- **SecurityAlerts**: Anomaly detection results at each timestamp

## References

- **FFmpeg Documentation**: https://ffmpeg.org/documentation.html
- **Ring API Entities**: Ring.Api.Video.Metadata.Models
- **System.IO.Abstractions**: Platform-agnostic file operations
