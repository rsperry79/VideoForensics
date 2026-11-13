# Plan: client hosts stop referencing the data layer

Status: draft, not started. Follows from U5 in `plans/db-access.md`.

## Goal
Client hosts (Ui.Shared, MauiApp, and any MCP bridge) reference only `Api.Contracts` and `Client.Common`. They never reference `data.*`, providers, or `Client.Core`. Every call they make to server data goes over `/api/v1` with the paired-device bearer token (client-split rule).

## Why
- Ui.Shared references `Client.Core` and `Client.Common`, and `Client.Common` references `data.common`. A client build therefore pulls in the data layer.
- Ui.Shared injects data-layer repository interfaces directly: `IDeviceRepository`, `IMediaItemRepository`, `IEventRepository`, `ICaseRepository`, `IMediaFlagRepository`, `IUserRepository`, `ILocationRepository`, `ILegalHoldRepository`, `IJammingRepository`, `IProviderAccountRepository`, `IOperatorPreferencesRepository`, plus review loaders.
- Ui.Shared also uses `Client.Core` service types: `ConfigTools`, `DeviceControlService`, `ReportGenerationService`, `StepUpService`, and others.

## Constraints (from the client-split rule and the CLAUDE.md invariants)
- Interface names do not change. Each `Remote*` class implements the same interface as the local one, so `.razor` files need no edits at cutover.
- Wire calls use `Api.Contracts` DTOs on `/api/v1/...` routes. DTO mapping is `entity.ToDto()` and `dto.ToDomain()` only.
- `Remote*` classes take and forward `CancellationToken`.
- No new auth mechanism per class: the paired-device bearer token.
- Ui.Shared cannot reference `Hosting`, because `Hosting` references Ui.Shared.
- Failing test first (tdd). Haiku subagents for implementation files, except where the user approves otherwise. Verify each result by reading the diff and building.

## Decisions (resolved 2026-11-13)
1. Shared interfaces move to `Api.Contracts` (namespace unchanged). Client hosts already reference it, and it holds the DTOs. `Client.Common` cannot be the home while it still references `data.common` (U5a is unfinished).
2. One endpoint group per area under `/api/v1/<area>` (D1–D4), not one per interface.

## Units (each: failing test, then implement, then lite gate, then commit)
- [ ] D0. Boundary test: Ui.Shared csproj has no `Client.Core` or `data/` reference; no Ui.Shared `.cs` or `.razor` file injects a data-layer repository interface. Red now. Recorded red run required.
- [ ] D1-pre. Api.Contracts becomes data-free (decided 2026-11-13). Move the 22 `ToDto()`/`ToDomain()` mapping files that use `VideoForensics.Data.*` out of Api.Contracts into WebApp/Hosting, and drop its `data/common`, `data/core` and `Client.Common` ProjectReferences. Red run recorded 2026-11-13: `ApiContracts_ProjectFile_HasNoDataLayerOrClientCommonReference` and `ApiContracts_Source_DoesNotUseDataLayerNamespaces` fail (22 files; 3 csproj references).
- [ ] D1. Device and media group: `IDeviceRepository`, `IMediaItemRepository`, `IMediaFlagRepository`. Decided 2026-11-13: interfaces move to Api.Contracts with DTO signatures, not entities. The entity-typed signatures cannot move, since `Device`, `MediaItem` and `MediaFlag` live in the data layer. Consumers switch to DTOs. Endpoints, `Remote*` classes, DI swap. Starts after D1-pre.
- [ ] D2. Event and review group: `IEventRepository`, review loaders.
- [ ] D3. Case, legal hold, and user group: `ICaseRepository`, `ILegalHoldRepository`, `IUserRepository`.
- [ ] D4. Location, provider account, operator preferences, jamming: the remaining repositories.
- [ ] D5. `Client.Core` services used by Ui.Shared (`ConfigTools`, `DeviceControlService`, `ReportGenerationService`, `StepUpService`, others). One unit per service.
- [ ] D6. Remove the `Client.Core` and `data/` references from Ui.Shared's csproj. D0 goes green.
- [ ] D7. MauiApp: remove its data-layer references. Verify the MAUI build.

## Risks
- Each repository group adds endpoints and an auth surface. Each needs the same step-up and rate-limit rules the existing backup endpoints use.
- Ui.Shared pages may rely on synchronous or in-process behaviour (for example entity change tracking). Check each page in its unit.
- The Ui.Shared test projects also use repositories. They must move in the same unit as the code they test.
- D0 red run recorded 2026-11-13: ClientDecoupleBoundaryTests fails on the Ui.Shared csproj Client.Core reference and on 49 Ui.Shared files that inject repositories.
