---
paths:
  - "src/client/**"
---

# Client/server split

Applies to MauiApp, Ui.Shared, Client.MCP, and any MCP bridge. Not WebApp (it is the server).

- Client hosts must not reference `providers-common`, `providers-core`, concrete providers, or `data.*` in their `.csproj`. Allowed: `VideoForensics.Api.Contracts`, `VideoForensics.Hosting`.
- Interface names do not change. `Remote*` classes implement the same interface as local ones, so `.razor` files need no edits at cutover.
- Wire calls use `Api.Contracts` DTOs on versioned `/api/v1/...` routes.
- `Remote*` classes take and forward `CancellationToken`.
- Every outgoing call carries the paired-device bearer token. Do not add a new auth mechanism per class.
- Never persist the local server address; only the cached Internet URL is stored. Discover local fresh (e.g. mDNS).
- No stdio MCP process may call `AddVideoForensicsDataLayer()` or `AddVideoForensicsServerCore()`.
- DTO mapping: `entity.ToDto()` and `dto.ToDomain()` only.
