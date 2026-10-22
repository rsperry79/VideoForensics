---
paths:
  - "**/Setup.razor"
  - "**/SetupEndpoints.cs"
  - "**/AuthGate.razor"
  - "**/VideoForensicsHostingExtensions.cs"
---

# First-run admin setup

- Empty database: `AuthGate.razor` redirects to `/setup` when `IOperatorRepository.IsEmptyAsync()`. The installer picks username and password. No forced change.
- `SetupEndpoints.cs` returns 403 once any operator exists. It cannot create a second SuperAdmin.
- Headless deployments: set `VIDEOFORENSICS_ENABLE_DEFAULT_ADMIN=true` before first start. `InitializeVideoForensicsDataAsync()` then creates `admin` / `ChangeMe123!` (SuperAdmin, MustChangePassword). Off by default. Idempotent; never overrides a `/setup` admin.
- The password constant `DefaultSuperAdminPassword` lives in `VideoForensicsHostingExtensions.cs`. Keep this note and the constant in sync.
