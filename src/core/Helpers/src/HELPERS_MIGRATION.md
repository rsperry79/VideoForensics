# Platform-Agnostic Helpers Library

## Overview

A new `VideoForensics.Providers.Common.Helpers` library has been created to house platform-agnostic utility code that can be reused across multiple provider implementations.

## Project Structure

```
src/core/helpers/
├── core.helpers/                            # Main library
│   ├── Contracts/
│   │   ├── IJsonSerializer.cs
│   │   ├── IMediaValidator.cs
│   │   └── IPlatformDirectoryService.cs
│   ├── Json/
│   │   └── JsonSerializer.cs               # Migrated from Ring
│   ├── Media/
│   │   └── MediaValidator.cs               # Migrated from Ring
│   ├── Platform/
│   │   └── PlatformDirectoryService.cs     # Migrated from Ring
│   ├── VideoForensics.Providers.Common.Helpers.csproj
│   └── HELPERS_MIGRATION.md                # This file
│
├── core.helpers-tests/                     # Test library (separate)
    ├── Json/
    │   └── JsonSerializerTests.cs
    ├── Media/
    │   └── MediaValidatorTests.cs
    ├── Platform/
    │   └── PlatformDirectoryServiceTests.cs
    └── VideoForensics.Providers.Common.Helpers.Tests.csproj
```

## Interfaces & Implementations

### JSON Converters
**Namespace:** `VideoForensics.Providers.Common.Helpers.Json.Converters`

Flexible JSON converters handle APIs that return inconsistent types for the same field:

- **FlexibleStringConverter** — Converts JSON string, number, or boolean to string
- **FlexibleBooleanConverter** — Accepts 0, 1, "true", "false" (case-insensitive)
- **FlexibleDecimalConverter** — Converts number or string to nullable decimal
- **FlexibleDoubleConverter** — Converts number or string to nullable double  
- **FlexibleIntConverter** — Converts number or string to nullable integer

**Example:**
```csharp
var options = new JsonSerializerOptions
{
    Converters = 
    {
        new FlexibleBooleanConverter(),
        new FlexibleDecimalConverter(),
        new FlexibleDoubleConverter(),
        new FlexibleIntConverter()
    }
};

// Now deserialize APIs with inconsistent types:
var result = JsonSerializer.Deserialize<MyType>(json, options);
```

---

### 1. IJsonSerializer
**Namespace:** `VideoForensics.Providers.Common.Helpers.Contracts`

Provides JSON serialization with three modes:
- **Default**: Compact JSON with safe escaping (for storage/transmission)
- **Pretty**: Indented JSON (for logging/display)
- **Raw**: Unsafe escaping (for API responses)

**Migrated from:** `VideoForensics.Providers.Ring.JsonUtil`

**Usage:**
```csharp
IJsonSerializer serializer = new JsonSerializer();
var json = serializer.Serialize(obj, JsonSerializationMode.Pretty);
var obj = serializer.Deserialize<MyType>(json);
```

### 2. IMediaValidator
**Namespace:** `VideoForensics.Providers.Common.Helpers.Contracts`

Validates media files exist and optionally match expected sizes.

**Migrated from:** `VideoForensics.Providers.Ring.DownloadHelper`

**Usage:**
```csharp
IMediaValidator validator = new MediaValidator();
bool isValid = validator.ValidateMediaExists(filePath, expectedSize: 1024000);
```

### 3. IPlatformDirectoryService
**Namespace:** `VideoForensics.Providers.Common.Helpers.Contracts`

Provides platform-agnostic access to standard application directories (data, logs, config).

**Features:**
- Cross-platform support (Windows, macOS, Linux)
- XDG Base Directory support on Linux
- Consistent directory structure across platforms

**Migrated from:** `VideoForensics.Providers.Ring.Common.PlatformDirectoryService`

**Usage:**
```csharp
IPlatformDirectoryService dirService = new PlatformDirectoryService();
string appData = dirService.GetApplicationDataDirectory();
string logs = dirService.GetLogsDirectory();
string config = dirService.GetConfigDirectory();
```

## Test Coverage

All interfaces are tested with xUnit:
- **JsonSerializerTests** (8 tests): Serialization modes, null handling, round-trip
- **MediaValidatorTests** (8 tests): File existence, size validation, edge cases
- **PlatformDirectoryServiceTests** (9 tests): Path validity, consistency across platforms

**Test Results:**
```
Passed! - Failed: 0, Passed: 25, Skipped: 0
```

## Integration Steps

### For Ring Provider
1. **Update Ring.csproj** to reference the new helpers library
2. **Remove** the old implementation files from Ring:
   - `src/providers/ring/core/JsonUtil.cs`
   - `src/providers/ring/core/DownloadHelper.cs`
   - `src/providers/ring/common/PlatformDirectoryService.cs`
   - `src/providers/ring/common/Interfaces/IPlatformDirectoryService.cs`
   - `src/providers/ring/common/Converters/` (all converter files)
3. **Update imports** in Ring code:
   - Change `using VideoForensics.Providers.Ring;` → `using VideoForensics.Providers.Common.Helpers.Json;`
   - Change `using VideoForensics.Providers.Ring.Converters;` → `using VideoForensics.Providers.Common.Helpers.Json.Converters;`
   - Change `using VideoForensics.Providers.Ring.Common.Interfaces;` → `using VideoForensics.Providers.Common.Helpers.Contracts;`
4. **Update JsonSerializerOptions** in Ring code to use converters from common library
5. **Test** the Ring provider to ensure it still works correctly

### For New Providers (Wyze, etc.)
1. Add project reference to `VideoForensics.Providers.Common.Helpers`
2. Inject the interfaces via dependency injection:
   ```csharp
   services.AddSingleton<IJsonSerializer, JsonSerializer>();
   services.AddSingleton<IMediaValidator, MediaValidator>();
   services.AddSingleton<IPlatformDirectoryService, PlatformDirectoryService>();
   ```
3. Use them in your provider implementation without any Ring-specific dependencies

## Benefits

✅ **Reusability** - Use the same utilities across all providers
✅ **Testability** - All interfaces are fully tested
✅ **Maintainability** - Single source of truth for common utilities
✅ **Extensibility** - Easy to add new cross-provider utilities
✅ **Platform Support** - Already handles Windows, macOS, Linux correctly

## Completed Steps

✅ **1. Ring Provider Migration** — COMPLETE
  - Updated Ring.Api.Common.csproj with reference to helpers library
  - Migrated all 5 JSON converters to common library
  - Updated 4 entity files to use new converter names and imports
  - Removed old converter files and tests
  - **Build Status:** ✅ All Ring modules build successfully

## Next Steps

1. [INTEGRATION] Add unit tests for Ring's usage of the helpers (optional)
2. [NEW PROVIDERS] Use as template when implementing Wyze/Blue Iris/etc. providers
3. [DOCUMENTATION] Update architectural guidelines (CLAUDE.md) with helpers pattern

## Building & Testing

```bash
# Build main library
dotnet build src/core/helpers/VideoForensics.Providers.Common.Helpers.csproj

# Build tests
dotnet build src/core/helpers-tests/VideoForensics.Providers.Common.Helpers.Tests.csproj

# Run tests
dotnet test src/core/helpers-tests/VideoForensics.Providers.Common.Helpers.Tests.csproj
```
