---
name: provider-onboarding
description: How to add a new video provider (vendor integration) to VideoForensics: required services, base class, and tests. Use when adding, scaffolding, or wiring a new provider.
---

# Adding a provider

- Do not create new interfaces. Reuse `VideoForensics.Providers.Common.Contracts.*` (`src/core/providers/common/Contracts/`).
- Create `src/providers/<vendor>/provider/` with four services:
  - `<Vendor>AuthService : IProviderAuthService`
  - `<Vendor>DeviceDiscoveryService : IDeviceDiscoveryService`
  - `<Vendor>MediaDownloadService : IMediaDownloadService`
  - `<Vendor>EventAndConfigService : IEventAndConfigService`
- Implement `<Vendor>VideoProvider : BaseVideoProvider`.
- Add tests in the project's `tests/` subfolder.
