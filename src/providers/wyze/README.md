# Wyze Provider Implementation

## Overview

This directory contains placeholder libraries for the Wyze camera provider implementation. The structure mirrors the Ring provider pattern to ensure consistency across multi-provider support.

## Directory Structure

```
wyze/
├── api/                    # Main Wyze API client
├── auth/                   # Authentication and credential management
├── common/                 # Common types and models
├── core/                   # Core functionality (device discovery, etc.)
├── utils/                  # Utility functions and extensions
├── tests/                  # Test projects (one per module)
└── README.md              # This file
```

## Module Descriptions

### `api/` - Wyze.Api
Main API client entry point. Contains:
- `WyzeApiClient` - Primary client class for Wyze API interactions
- `Package.cs` - Version information

**Status:** 🔧 Placeholder

### `auth/` - Wyze.Api.Auth
Authentication and credential handling. Contains:
- `IWyzeAuthService` - Authentication service interface
- `WyzeAuthService` - Authentication service implementation
- `WyzeCredentials` - Credential data model

**Status:** 🔧 Placeholder

### `common/` - Wyze.Api.Common
Platform-agnostic common types and models. Contains:
- `WyzeDevice` - Device data model

**Status:** 🔧 Placeholder

### `core/` - Wyze.Api.Core
Core functionality implementations. Contains:
- `WyzeDeviceDiscovery` - Device discovery implementation

**Status:** 🔧 Placeholder

### `utils/` - Wyze.Api.Utils
Utility functions and extensions. Contains:
- `WyzeHttpClientExtensions` - HTTP client helper methods

**Status:** 🔧 Placeholder

## Next Steps

1. **Complete Authentication Module** (`auth/`)
   - Implement OAuth/API key authentication flow
   - Create credential encryption utilities
   - Add session management

2. **Implement Device Discovery** (`core/`)
   - Fetch device list from Wyze API
   - Parse device metadata
   - Implement device status tracking

3. **Add Media Operations** (new module or core)
   - Video download implementation
   - Event retrieval
   - Configuration management

4. **Create Comprehensive Tests**
   - Unit tests for each service
   - Integration tests with mocked Wyze API
   - Test data fixtures

5. **Update Solution File**
   - Add project references to Ring.Api.sln
   - Ensure build system recognizes new modules

## Building

```bash
cd external/RingApi/src
dotnet build providers/wyze/
```

## Testing

Once test projects are created:

```bash
dotnet test providers/wyze/
```

## Architecture Notes

- All modules inherit from the Ring provider pattern
- Public APIs are defined by interfaces
- Dependencies flow upward (api -> core -> auth -> common)
- Strong naming using shared signing key
- NuGet package generation enabled on build

## Implementation Guidelines

When implementing functionality:
1. Keep interfaces platform-agnostic (defined in `common/`)
2. Use async/await for all I/O operations
3. Support cancellation tokens
4. Implement proper error handling and logging
5. Follow the test-first approach
6. Maintain compatibility with VideoForensics.Providers.* pattern

## Dependencies

- Microsoft.CodeAnalysis (for auth module)
- System.Security.Cryptography.ProtectedData (for credential encryption)
- Core helpers and utilities from `external/RingApi/src/core/`
