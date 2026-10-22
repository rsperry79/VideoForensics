# Project layout

Projects nest under `src/` by layer. Each has a `tests/` subfolder.

```
src/
  core/providers/providers-common/   # Provider contracts (Contracts/)
  core/providers/providers-core/     # Base classes (BaseVideoProvider)
  providers/ring/{provider,auth,common,core,snapshots,utils,video}/
  providers/wyze/{provider,auth,common,core,utils}/
  data/{common,core,database,database/sqlite}/
  client/                            # MAUI, Ui.Shared, MCP, Hosting, WebApp
```

- `archive/VideoForensics/`: retired console app, not in the solution. Do not read unless asked.
- Prefer Visual Studio MCP `GetSolutionTree` over `find`/`Glob` for structure.
