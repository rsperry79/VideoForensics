---
paths:
  - "src/providers/**"
  - "src/core/providers/**"
---

# Adding a provider

- Do not create new interfaces. Reuse `VideoForensics.Providers.Common.Contracts.*` (`src/core/providers/providers-common/Contracts/`).
- Create `src/providers/<vendor>/provider/` with four services:
  - `<Vendor>AuthService : IProviderAuthService`
  - `<Vendor>DeviceDiscoveryService : IDeviceDiscoveryService`
  - `<Vendor>MediaDownloadService : IMediaDownloadService`
  - `<Vendor>EventAndConfigService : IEventAndConfigService`
- Implement `<Vendor>VideoProvider : BaseVideoProvider`.
- Add tests in the project's `tests/` subfolder.
