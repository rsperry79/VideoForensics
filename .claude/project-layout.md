# Project layout

Projects nest under `src/` by layer. Each has a `tests/` subfolder (except `providers/ring/testing`, a test-support library).

```
src/
  core/
    api/  forensics/  helpers/  logging/  telemetry/
    providers/common/                # Provider contracts (Contracts/)
    providers/core/                  # Base classes (BaseVideoProvider)
  providers/
    ring/{provider,auth,common,core,forensics,snapshots,snapshots/metadata,
          utils,video,video/metadata,testing}/
    wyze/{provider,auth,common,core,utils}/
    uniview/provider/
  data/{common,core,database,database/sqlite}/
  client/
    common/  core/  host/  maui/  mcp/  platform/  ui/  web/
    host/VideoForensics.Hosting/     ui/VideoForensics.Ui.Shared/     web/VideoForensics.WebApp/
```

- `archive/VideoForensics/`: retired console app, not in the solution. Do not read unless asked.
- Prefer Visual Studio MCP `GetSolutionTree` over `find`/`Glob` for structure.
