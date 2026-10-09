# MCP per-user and per-case data scoping

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN (conditional on a product decision). No scoping exists in MCP tools.

**Source**: `plans/archive/simple-mode-embedded-mcp-chat.md` line 26 ("Known limitation, intentionally out of scope": MCP tools take a bare `deviceId`/`locationId` with no per-user/case scoping; whole DB is one tenant; multi-location/multi-victim isolation is a separate follow-up).

## Verdict

STILL OPEN. Grepped `McpServerTool` on origin/dev: tools in `src/client/web/VideoForensics.WebApp/Mcp/Tools/` (`JammingTools`, `TimelineTools`, `AuditTrailTools`, `CorrelationTools`, `IntegrityTools`, `SecurityEventTools`, base `ForensicsToolBase`) take caller-supplied ids and query repositories unfiltered. Only `SecurityEventTools` reads caller claims (`VideoForensicsClaimTypes.OperatorId`, role check) and that is self-service vs SuperAdmin, not data scoping. A partial building block exists: `ForensicCase` with `CaseDevice` (CaseId, DeviceId), `CaseItem`, and `ICaseRepository.SetScopeAsync`/`GetDeviceIdsAsync` (case time window + device set), but nothing links an `Operator` (`Entities/Operator.cs`: Role, no case/location/owner column) to cases, devices or locations, and no MCP tool consults cases.

## Decision required

Question for the user: must one install ever host more than one household/victim/location whose data must be hidden from the other's accounts (and from their MCP chat)?

- **No (single install = single household/investigation).** Cost: none now. Residual risk: any approved operator, including ReadOnly Simple Mode victims, can enumerate all devices via MCP; acceptable only while the deployment is one household. Document the assumption in the deployment docs/UI setup text.
- **Yes, per-case isolation (investigator assigns operators to cases).** Cost: M-L. Reuse `ForensicCase` + `CaseDevice` as the scope boundary; add operator-to-case assignment; filter every MCP tool and the matching REST/repository reads by the caller's allowed device set and case time window. Touches every tool and needs a migration.
- **Yes, per-location/tenant isolation (separate installs-in-one).** Cost: L-XL. Adds a tenant key on Device/Location/Event and global query filters across the data layer plus all API endpoints. Not recommended; running a separate install per household is far cheaper.

## Scope if yes

Minimum change (per-case option): 
1. New join entity `OperatorCaseAssignment` (OperatorId, CaseId, AssignedAtUtc) plus repository interface in `data.common/Contracts`, EF migration in `data.database.sqlite`.
2. A contract `IMcpScopeResolver` (in a Contracts folder) returning, for the caller's claims, an `AllowedScope` (null = unrestricted for Admin/SuperAdmin; otherwise device-id set and time window derived from assigned cases via `ICaseRepository.GetDeviceIdsAsync`/case window).
3. `ForensicsToolBase` gets a helper that resolves scope from `HttpContext` claims (`VideoForensicsClaimTypes.OperatorId`) and rejects/filters; every tool intersects requested `deviceId`/`locationId` and time range with scope (fail closed: empty scope returns nothing, out-of-scope id returns the same error as not-found).
4. Admin UI to assign operators to cases (localized per CLAUDE.md) only after the server side is proven; defer if not needed at first.
Restrict to ReadOnly/Review roles first; Admin/SuperAdmin stay unrestricted so existing investigator workflows do not change.

## Code and file touchpoints

| file | change | why |
|---|---|---|
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/ForensicsToolBase.cs` | add scope-resolution helper | single enforcement point for all tools |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/JammingTools.cs` | filter/validate deviceId, locationId | currently unfiltered |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/TimelineTools.cs` | same | currently unfiltered |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/AuditTrailTools.cs` | same (or restrict to Admin+) | audit data spans cases |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/CorrelationTools.cs` | same | cross-device correlation can leak other cases |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/IntegrityTools.cs` | same | media ids are cross-case |
| `src/client/web/VideoForensics.WebApp/Mcp/Tools/SecurityEventTools.cs` | none expected (already claim-scoped) | reference pattern |
| `src/data/common/data.common/Contracts/ICaseRepository.cs` | reuse `GetDeviceIdsAsync`; possibly add list-by-operator | scope source |
| `src/data/common/data.common/Entities/CaseDevice.cs` | reuse | device-to-case mapping |
| `src/data/common/data.common/Entities/Operator.cs` | no column; use new join entity instead | avoids widening Operator |
| `src/data/database/sqlite/data.database.sqlite/Migrations/` | new migration for assignment table | schema |

## Test plan

Write first; all fail initially because no scoping exists (expected failure: out-of-scope device data is returned instead of denied).
- New tests in `src/client/web/VideoForensics.WebApp.Tests/` (xUnit v3 + Moq), e.g. `TimelineTools_DeviceOutsideCaseScope_ReturnsNotFound`, `TimelineTools_AdminCaller_IsUnrestricted`, `JammingTools_ReadOnlyWithNoAssignedCase_ReturnsEmpty`, `ForensicsToolBase_MissingOperatorClaim_FailsClosed`, one per tool for out-of-scope id.
- Resolver tests: `McpScopeResolver_AssignedCases_ReturnsUnionOfCaseDevices`, `McpScopeResolver_ClosedCase_...` (decide semantics).
- Data tests in the existing data.database.sqlite tests project for the assignment repository.
- Run scoped with `dotnet test --filter` on the touched classes only.

## Risks and open questions

- Fail-open bugs: a missed tool leaks data; add a reflection test that every `[McpServerTool]` method goes through the scope helper.
- Scope must also cover non-MCP paths (REST `/api/v1`, Blazor repositories); MCP-only scoping is false assurance if victims can use the UI/API to see everything.
- Semantics of closed cases, overlapping cases, case time windows, and devices in no case.
- Chain-of-custody: access denials should be audit-logged.
- Per CLAUDE.md the stdio bridge must not touch the data layer; enforcement stays server-side in WebApp.

## Priority recommendation

**Low (conditional).** Product value is nil for the single-household model; security exposure is real only if victims and investigators with differing entitlements share one install, and then it is also an API/UI problem, not MCP alone; effort **L** (M for MCP-only), do only if the decision is "yes".

## Implementation dispatch

Tests-first subagent for resolver + base helper, then one subagent per 1-2 tools, then a data/migration subagent; verify each with the lite gate (scoped build/test).
