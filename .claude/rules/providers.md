---
paths:
  - "src/providers/**"
  - "src/core/providers/**"
  - "src/data/**"
---

# Providers and data

## Adding a provider
- Do not create new interfaces. Reuse `VideoForensics.Providers.Common.Contracts.*` (`src/core/providers/common/Contracts/`).
- Create `src/providers/<vendor>/provider/` with four services:
  - `<Vendor>AuthService : IProviderAuthService`
  - `<Vendor>DeviceDiscoveryService : IDeviceDiscoveryService`
  - `<Vendor>MediaDownloadService : IMediaDownloadService`
  - `<Vendor>EventAndConfigService : IEventAndConfigService`
- Implement `<Vendor>VideoProvider : BaseVideoProvider`.
- Add tests in the project's `tests/` subfolder.

## Data
- Every provider API field is parsed and stored in typed columns or tables. Design the schema first; never store JSON to dodge a schema gap.
- Raw JSON only for audit trail or fields that cannot be normalized, in an optional `MetadataJson` column. No unstructured JSON in core domain tables (events, devices, locations).
- Do not scope globally unique provider resources to an account (e.g. `ProviderLocationId`). Use junction tables for multi-account sharing.
- Run filtering, sorting, paging, counting, and aggregation on the server (EF query → SQL). Do not `ToListAsync()` and then filter, sort, or page in memory. Pull to the client only when the logic cannot translate to SQL; say why in a comment.
