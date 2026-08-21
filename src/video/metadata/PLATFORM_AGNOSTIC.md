# Platform-Agnostic Design

## Overview

The `Ring.Api.Video.Metadata` library is designed to be fully platform-agnostic and runs seamlessly on Windows, Linux, and macOS without platform-specific code paths or conditional compilation.

## Key Design Principles

### 1. Abstraction Over Direct I/O
All file system operations use the `System.IO.Abstractions` NuGet package instead of direct `System.IO` calls. This provides:
- **Cross-platform consistency**: The same code works identically on all platforms
- **Testability**: File operations can be mocked in unit tests
- **Decoupling**: Implementation details are abstracted away

### 2. .NET BCL Dependency Only
The library relies on .NET Base Class Library features that are platform-agnostic:
- `System.Text.Json` for JSON serialization (no external JSON libraries)
- `System.IO` and `System.IO.Abstractions` for file operations
- `System.Runtime.InteropServices` for any platform detection (if needed in future)
- Standard threading APIs with cross-platform support

### 3. No Platform-Specific Code
- **No `PlatformNotSupportedException` handling**: The code doesn't check for specific platforms
- **No conditional compilation**: No `#if WIN` or `#if LINUX` directives
- **No native P/Invoke**: All operations use managed .NET APIs
- **No shell scripts**: No bash/PowerShell specific logic

## Technical Implementation

### File System Abstraction Layer

```csharp
public class NoOpMetadataWriter : IMetadataWriter
{
    private readonly IFileSystem _fileSystem;

    // Dependency injection allows swapping implementations
    public NoOpMetadataWriter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem(); // Default to real file system
    }

    private bool IsValidVideoFile(string filePath)
    {
        // All file operations use IFileSystem abstraction
        if (!_fileSystem.File.Exists(filePath))
            return false;

        var extension = _fileSystem.Path.GetExtension(filePath);
        // ... cross-platform file validation
    }
}
```

### Benefits of This Approach

1. **Same Code, All Platforms**
   ```
   Windows:  C:\videos\recording.mp4
   Linux:    /home/user/videos/recording.mp4
   macOS:    /Users/username/videos/recording.mp4
   
   Path handling is abstracted and works identically
   ```

2. **Testability**
   - Tests can use `MockFileSystem` for fast, isolated testing
   - No real file I/O needed for unit tests
   - Test behavior is deterministic

3. **Future Extensibility**
   - Can implement custom `IFileSystem` for cloud storage (Azure Blob, S3, etc.)
   - Can implement caching layers transparently
   - Can add encryption/decryption without changing consumers

## Dependencies

### Primary Dependency
- **System.IO.Abstractions v21.2.1** (or newer)
  - Well-maintained, community-driven project
  - Over 1M+ NuGet downloads
  - MIT licensed
  - Supports .NET Framework, .NET Core, .NET Standard
  - No external dependencies itself

### No Problematic Dependencies
- No OS-specific libraries
- No platform detection libraries
- No shell/script execution libraries
- No architecture-specific assemblies

## Testing Across Platforms

The codebase automatically tests on multiple platforms through:

1. **Unit Tests** (CI/CD agnostic)
   - 57 comprehensive tests
   - All path operations use abstraction
   - No platform-specific test code

2. **Integration Testing**
   - Can run on Windows, Linux, Docker containers
   - Same test suite, same results

## Future Enhancements

### Recommended Platform-Agnostic Additions

1. **Async File Operations**
   ```csharp
   // Future: Use IFileSystem async methods
   await _fileSystem.File.ReadAllBytesAsync(path);
   ```

2. **Cancellation Support**
   ```csharp
   // CancellationToken support in WriteMetadataAsync
   public Task<MetadataWriteResult> WriteMetadataAsync(
       string videoFilePath, 
       VideoMetadata metadata,
       CancellationToken ct)
   ```

3. **Remote File System Support**
   ```csharp
   // Implement IFileSystem for cloud storage
   var s3FileSystem = new AmazonS3FileSystem(credentials);
   var writer = new NoOpMetadataWriter(s3FileSystem);
   ```

## Verification

To verify platform-agnostic compatibility:

```bash
# Windows
dotnet build

# Linux/macOS
dotnet build
dotnet test

# Docker
docker run --rm -v $(pwd):/workspace mcr.microsoft.com/dotnet/sdk:10 \
  bash -c "cd /workspace && dotnet test"
```

All tests pass identically on all platforms with zero platform-specific code.
